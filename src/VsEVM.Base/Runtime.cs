namespace VsEVM;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Core;
using Serilog.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public abstract class Runtime
{
    #region Constructors
    static Runtime()
    {
        AppDomain.CurrentDomain.UnhandledException += AppDomain_UnhandledException;
        EntryAssembly = Assembly.GetEntryAssembly();
        IsUnitTestRun = EntryAssembly?.FullName?.StartsWith("testhost") ?? false;
        SessionId = Rng.Next(0, 99999);            
    }

    public Runtime(CancellationToken ct)
    {
        Ct = ct;
    }

    public Runtime() : this(Cts.Token) { }
    #endregion

    #region Properties
    public static bool RuntimeInitialized { get; protected set; }

    public static bool DebugEnabled { get; set; }

    public static bool InteractiveConsole { get; set; } = false;

    public static string PathSeparator { get; } = Environment.OSVersion.Platform == PlatformID.Win32NT ? "\\" : "/";

    public static string ToolName { get; set; } = "VsEVM";
        
    public static string LogName { get; set; } = "BASE";

    public static string UserHomeDir => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string AppDataDir => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static string VsEVMDir => Path.Combine(AppDataDir, "VsEVM");

    public static string LocalAppDataDir => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static Random Rng { get; } = new Random();

    public static int SessionId { get; protected set; }

    public static CancellationTokenSource Cts { get; } = new CancellationTokenSource();

    public static CancellationToken Ct { get; protected set; } = Cts.Token;

    public static Assembly? EntryAssembly { get; protected set; }

    public static string AssemblyLocation { get; } = Path.GetDirectoryName(Assembly.GetAssembly(typeof(Runtime))!.Location)!;

    public static Version AssemblyVersion { get; } = Assembly.GetAssembly(typeof(Runtime))!.GetName().Version!;
        
    public static string CurentDirectory => Directory.GetCurrentDirectory();

    public static bool IsUnitTestRun { get; set; }

    public static string RunFile => Path.Combine(VsEVMDir, ToolName + ".run");

    public virtual bool Initialized { get; protected set; }

    public CancellationToken CancellationToken { get; protected set; }
    #endregion

    #region Methods
    public static void Initialize(string toolname, string logname, bool debug, ILoggerFactory lf, ILoggerProvider lp)
    {
        lock (__lock)
        {
            Info("Initialize called on thread id {0}.", Thread.CurrentThread.ManagedThreadId);
            if (RuntimeInitialized)
            {
                Info("Runtime already initialized.");
                return;
            }
            ToolName = toolname;
            LogName = logname;
            DebugEnabled = debug;
            loggerFactory = lf;
            loggerProvider = lp;
            logger = lf.CreateLogger(toolname);
            RuntimeInitialized = true;
        }
    }

    public static void Initialize(string toolname, string logname, bool debug = false) => Initialize(toolname, logname, debug, NullLoggerFactory.Instance, NullLoggerProvider.Instance);
        
    public static void WithFileLogging(string toolname, string logname, bool debug, string? logdir = null)
    {        
        var filePath= logdir is null ? Path.Combine(AssemblyLocation, toolname + "-" + logname + ".log") : Path.Combine(logdir, toolname + "-" + logname + ".log");
        var logger = new LoggerConfiguration()
             .Enrich.FromLogContext()
             .MinimumLevel.Is(debug ? Serilog.Events.LogEventLevel.Verbose : Serilog.Events.LogEventLevel.Information)    
             .WriteTo.File(filePath)
             .CreateLogger();
        var lf = new SerilogLoggerFactory(logger);
        var lp = new SerilogLoggerProvider(logger, false);        
        Initialize(toolname, logname, debug, lf, lp);
    }

    public static void WithFileAndConsoleLogging(string toolname, string logname, bool debug, string? logdir = null)
    {
        var filePath = logdir is null ? Path.Combine(AssemblyLocation, toolname + "-" + logname + ".log") : Path.Combine(logdir, toolname + "-" + logname + ".log");
        var logger = new LoggerConfiguration()
             .Enrich.FromLogContext()
             .MinimumLevel.Is(debug ? Serilog.Events.LogEventLevel.Verbose : Serilog.Events.LogEventLevel.Information)
             .WriteTo.File(filePath)
             .WriteTo.Console()
             .CreateLogger();
        var lf = new SerilogLoggerFactory(logger);
        var lp = new SerilogLoggerProvider(logger, false);
        Initialize(toolname, logname, debug, lf, lp);
    }

    [DebuggerStepThrough]
    public static void Info(string messageTemplate, params object[] args) => logger.LogInformation(messageTemplate, args);

    [DebuggerStepThrough]
    public static void Debug(string messageTemplate, params object[] args) => logger.LogDebug(messageTemplate, args);

    [DebuggerStepThrough]
    public static void Error(string messageTemplate, params object[] args) => logger.LogError(messageTemplate, args);

    [DebuggerStepThrough]
    public static void Error(Exception ex, string messageTemplate, params object[] args) => logger.LogError(ex, messageTemplate, args);

    [DebuggerStepThrough]
    public static void Warn(string messageTemplate, params object[] args) => logger.LogWarning(messageTemplate, args);

    [DebuggerStepThrough]
    public static void Fatal(string messageTemplate, params object[] args) => logger.LogCritical(messageTemplate, args);

    [DebuggerStepThrough]
    public static LoggerOp Begin(string messageTemplate, params object[] args) => new LoggerOp(logger, messageTemplate, args);

    [DebuggerStepThrough]
    public static string FailIfFileDoesNotExist(string filePath)
    {
        if (filePath.StartsWith("http://") || filePath.StartsWith("https://"))
        {
            return filePath;
        }
        else if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(filePath);
        }
        else return filePath;
    }
    
    [DebuggerStepThrough]
    public static string WarnIfFileExists(string filename)
    {
        if (File.Exists(filename)) Warn("File {0} exists, overwriting...", filename);
        return filename;
    }

    [DebuggerStepThrough]
    public static string CreateIfDirectoryDoesNotExist(string dirPath)
    {
        if (!Directory.Exists(dirPath))
        {
            Directory.CreateDirectory(dirPath);
        }
        return dirPath;
    }

    [DebuggerStepThrough]
    public static object? GetProp(object o, string name)
    {
        PropertyInfo[] properties = o.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return properties.FirstOrDefault(x => x.Name == name)?.GetValue(o);
    }

    private static void AppDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Error((Exception)e.ExceptionObject, "Unhandled runtime error occurred.");   
    }



    public static string? RunCmd(string cmdName, string arguments = "", string? workingDir = null, DataReceivedEventHandler? outputHandler = null, DataReceivedEventHandler? errorHandler = null,
        bool checkExists = true, bool isNETFxTool = false, bool isNETCoreTool = false)
    {
        if (checkExists && !(File.Exists(cmdName) || (isNETCoreTool && File.Exists(cmdName.Replace(".exe", "")))))
        {
            Error("The executable {0} does not exist.", cmdName);
            return null;
        }
        using (Process p = new Process())
        {
            var output = new StringBuilder();
            var error = new StringBuilder();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardInput = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.CreateNoWindow = true;

            if (isNETFxTool && System.Environment.OSVersion.Platform == PlatformID.Unix)
            {
                p.StartInfo.FileName = "mono";
                p.StartInfo.Arguments = cmdName + " " + arguments;
            }
            else if (isNETCoreTool && System.Environment.OSVersion.Platform == PlatformID.Unix)
            {
                p.StartInfo.FileName = File.Exists(cmdName) ? cmdName : cmdName.Replace(".exe", "");
                p.StartInfo.Arguments = arguments;

            }
            else
            {
                p.StartInfo.FileName = cmdName;
                p.StartInfo.Arguments = arguments;
            }

            p.OutputDataReceived += (sender, e) =>
            {
                if (e.Data is not null)
                {
                    output.AppendLine(e.Data);
                    Debug(e.Data);
                    outputHandler?.Invoke(sender, e);
                }
            };
            p.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data is not null)
                {
                    error.AppendLine(e.Data);
                    Error(e.Data);
                    errorHandler?.Invoke(sender, e);
                }
            };
            if (workingDir is not null)
            {
                p.StartInfo.WorkingDirectory = workingDir;
            }
            Debug("Executing cmd {0} in working directory {1}.", cmdName + " " + arguments, p.StartInfo.WorkingDirectory);
            try
            {
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.WaitForExit();
                return error.ToString().IsNotEmpty() ? null : output.ToString();
            }

            catch (Exception ex)
            {
                Error(ex, "Error executing command {0} {1}", cmdName, arguments);
                return null;
            }
        }
    }

    public static Dictionary<string, object> RunCmd(string filename, string arguments, string workingdirectory)
    {
        ProcessStartInfo info = new ProcessStartInfo();
        info.FileName = filename;
        info.Arguments = arguments;
        info.WorkingDirectory = workingdirectory;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        var output = new Dictionary<string, object>();
        using (var process = new Process())
        {
            process.StartInfo = info;
            try
            {
                if (!process.Start())
                {
                    output["error"] = ("Could not start {file} {args} in {dir}.", info.FileName, info.Arguments, info.WorkingDirectory);
                    return output;
                }
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                if (stdout != null && stdout.Length > 0)
                {
                    output["stdout"] = stdout;
                }
                if (stderr != null && stderr.Length > 0)
                {
                    output["stderr"] = stderr;
                }
                return output;
            }
            catch (Exception ex)
            {
                output["exception"] = ex;
                return output;
            }
        }
    }

    public static async Task<Dictionary<string, object>> RunCmdAsync(string filename, string arguments, string workingdirectory)
    {
        ProcessStartInfo info = new ProcessStartInfo();
        info.FileName = filename;
        info.Arguments = arguments;
        info.WorkingDirectory = workingdirectory;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        var output = new Dictionary<string, object>();
        using (var process = new Process())
        {
            process.StartInfo = info;
            try
            {
                if (!process.Start())
                {
                    output["error"] = ("Could not start {file} {args} in {dir}.", info.FileName, info.Arguments, info.WorkingDirectory);
                    return output;
                }
                var stdout = await process.StandardOutput.ReadToEndAsync();
                var stderr = await process.StandardError.ReadToEndAsync();
                if (stdout != null && stdout.Length > 0)
                {
                    output["stdout"] = stdout;
                }
                if (stderr != null && stderr.Length > 0)
                {
                    output["stderr"] = stderr;
                }
                return output;
            }
            catch (Exception ex)
            {
                output["exception"] = ex;
                return output;
            }
        }
    }

    public static bool CheckRunCmdError(Dictionary<string, object> output) => output.ContainsKey("error") || output.ContainsKey("exception");

    public static string GetRunCmdError(Dictionary<string, object> output) => (output.ContainsKey("error") ? (string)output["error"] : "")
        + (output.ContainsKey("exception") ? (string)output["exception"] : "");

    public static bool CheckRunCmdOutput(Dictionary<string, object> output, string checktext)
    {
        if (output.ContainsKey("error") || output.ContainsKey("exception"))
        {
            if (output.ContainsKey("error"))
            {
                Error((string)output["error"]);
            }
            if (output.ContainsKey("exception"))
            {
                Error((Exception)output["exception"], "Exception thrown during process execution.");
            }
            return false;
        }
        else
        {
            if (output.ContainsKey("stderr"))
            {
                var stderr = (string)output["stderr"];
                Info(stderr);
            }
            if (output.ContainsKey("stdout"))
            {
                var stdout = (string)output["stdout"];
                Info(stdout);
                if (stdout.Contains(checktext))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
    }

    public static void CopyDirectory(string sourceDir, string destinationDir, bool recursive = false)
    {
        using var op = Begin("Copying {0} to {1}", sourceDir, destinationDir);
        // Get information about the source directory
        var dir = new DirectoryInfo(sourceDir);

        // Check if the source directory exists
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        // Cache directories before we start copying
        DirectoryInfo[] dirs = dir.GetDirectories();

        // Create the destination directory
        Directory.CreateDirectory(destinationDir);

        // Get the files in the source directory and copy to the destination directory
        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath);
        }

        // If recursive and copying subdirectories, recursively call this method
        if (recursive)
        {
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir, true);
            }
        }
        op.Complete();
    }

    public static async Task CopyDirectoryAsync(string sourceDir, string destinationDir, bool recursive = false)
    {
        using var op = Begin("Copying {0} to {1}", sourceDir, destinationDir);
        // Get information about the source directory
        var dir = new DirectoryInfo(sourceDir);

        // Check if the source directory exists
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        // Cache directories before we start copying
        DirectoryInfo[] dirs = dir.GetDirectories();

        // Create the destination directory
        if (!Directory.Exists(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
        }

        // Get the files in the source directory and copy to the destination directory
        foreach (var file in dir.GetFiles())
        {
            var of = Path.Combine(destinationDir, file.Name);
            using (FileStream sourceStream = file.Open(FileMode.Open))
            {
                using (FileStream destinationStream = File.Create(of))
                {
                    await sourceStream.CopyToAsync(destinationStream);
                }
            }
        }

        // If recursive and copying subdirectories, recursively call this method
        if (recursive)
        {
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                await CopyDirectoryAsync(subDir.FullName, newDestinationDir, true);
            }
        }
        op.Complete();
    }

    public static async Task CopyFileAsync(string src, string dst)
    {
        using (var srcStream = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.Read, 0x1000, useAsync: true))
        using (var dstStream = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.Write, 0x1000, useAsync: true))
        {
            await srcStream.CopyToAsync(dstStream);
        }
    }

    public static async Task<bool> DownloadFileAsync(string name, Uri downloadUrl, string downloadPath)
    {
#pragma warning disable SYSLIB0014 // Type or member is obsolete
        using (var op = Begin("Downloading {0} from {1} to {2}", name, downloadUrl, downloadPath))
        {
            using (var client = new WebClient())
            {
                try
                {
                    var b = await client.DownloadDataTaskAsync(downloadUrl);
                    if (b != null)
                    {
                        File.WriteAllBytes(downloadPath, b);
                        op.Complete();
                        return true;
                    }
                    else
                    {
                        op.Abandon();
                        Error("Downloading {file} to {path} from {url} did not return any data.", name, downloadPath, downloadUrl);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    op.Abandon();
                    Error(ex, "Exception thrown downloading {file} to {path} from {url}.", name, downloadPath, downloadUrl);
                    return false;
                }
            }
        }
#pragma warning restore SYSLIB0014 // Type or member is obsolete
    }

    public static string ViewFilePath(string path, string? relativeTo = null)
    {
        if (!DebugEnabled)
        {
            if (path is null)
            {
                return string.Empty;
            }
            else if (relativeTo is null)
            {
                return (Path.GetFileName(path) ?? path);
            }
            else
            {
                return (IOExtensions.GetRelativePath(relativeTo, path));
            }
        }
        else return path;
    }

    /// <summary>
    /// Returns a relative path string from a full path based on a base path
    /// provided.
    /// </summary>
    /// <param name="fullPath">The path to convert. Can be either a file or a directory</param>
    /// <param name="basePath">The base path on which relative processing is based. Should be a directory.</param>
    /// <returns>
    /// String of the relative path.
    /// 
    /// Examples of returned values:
    ///  test.txt, ..\test.txt, ..\..\..\test.txt, ., .., subdir\test.txt
    /// </returns>
    public static string GetWindowsRelativePath(string fullPath, string basePath)
    {
        // Require trailing backslash for path
        if (!basePath.EndsWith("\\"))
            basePath += "\\";

        Uri baseUri = new Uri(basePath);
        Uri fullUri = new Uri(fullPath);

        Uri relativeUri = baseUri.MakeRelativeUri(fullUri);

        // Uri's use forward slashes so convert back to backward slashes
        return relativeUri.ToString().Replace("/", "\\");

    }
    
    public static bool DownloadFile(string name, Uri downloadUrl, string downloadPath)
    {
#pragma warning disable SYSLIB0014 // Type or member is obsolete
        using (var op = Begin("Downloading {0} from {1} to {2}", name, downloadUrl, downloadPath))
        {
            WarnIfFileExists(downloadPath);
            using (var client = new WebClient())
            {
                client.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) =>
                {
                    Info("Received {b} bytes from of {t} for {p}.", e.BytesReceived, e.TotalBytesToReceive, downloadPath);
                        
                };
                client.DownloadDataCompleted += (object sender, DownloadDataCompletedEventArgs e) =>
                {

                };
                client.DownloadFile(downloadUrl, downloadPath);    
            }
            if (File.Exists(downloadPath)) 
            {
                op.Complete();
                return true;
            }
            else
            {
                Error("Did not locate file at {p}.", downloadPath);
                return false;
            }
        }
#pragma warning restore SYSLIB0014 // Type or member is obsolete
    }

    public static void VerifyNotNull(params object?[] objects)
    {             
        for(var i = 0; i < objects.Length;  i++)
        {
            if (objects[i] is null)
            {
                throw new ArgumentNullException($"Object at index {i} in args is null.");
            }
        }
    }

    public static string RandomString(int length)
    {
        const string pool = "abcdefghijklmnopqrstuvwxyz0123456789";
        var builder = new StringBuilder();

        for (var i = 0; i < length; i++)
        {
            var c = pool[Rng.Next(0, pool.Length)];
            builder.Append(c);
        }

        return builder.ToString();
    }

    public static IConfigurationRoot LoadConfigFile(string configFilePath, bool required = true) =>    
        new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(configFilePath, optional: !required, reloadOnChange: true)
                .Build();
     
    #endregion

    #region Fields
    public static Microsoft.Extensions.Logging.ILogger logger = NullLogger.Instance;   
    public static ILoggerFactory loggerFactory = NullLoggerFactory.Instance;
    public static ILoggerProvider loggerProvider = NullLoggerProvider.Instance; 
    protected static object __lock = new object();
    #endregion
}
