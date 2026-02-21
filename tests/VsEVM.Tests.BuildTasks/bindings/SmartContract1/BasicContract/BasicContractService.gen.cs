using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts;
using System.Threading;
using TestNS.BasicContract.ContractDefinition;

namespace TestNS.BasicContract
{
    public partial class BasicContractService: BasicContractServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, BasicContractDeployment basicContractDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<BasicContractDeployment>().SendRequestAndWaitForReceiptAsync(basicContractDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, BasicContractDeployment basicContractDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<BasicContractDeployment>().SendRequestAsync(basicContractDeployment);
        }

        public static async Task<BasicContractService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, BasicContractDeployment basicContractDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, basicContractDeployment, cancellationTokenSource);
            return new BasicContractService(web3, receipt.ContractAddress);
        }

        public BasicContractService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class BasicContractServiceBase: ContractWeb3ServiceBase
    {

        public BasicContractServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> BasicFunctionRequestAsync(BasicFunctionFunction basicFunctionFunction)
        {
             return ContractHandler.SendRequestAsync(basicFunctionFunction);
        }

        public virtual Task<TransactionReceipt> BasicFunctionRequestAndWaitForReceiptAsync(BasicFunctionFunction basicFunctionFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(basicFunctionFunction, cancellationToken);
        }

        public virtual Task<string> BasicFunctionRequestAsync(BigInteger funcArg)
        {
            var basicFunctionFunction = new BasicFunctionFunction();
                basicFunctionFunction.FuncArg = funcArg;
            
             return ContractHandler.SendRequestAsync(basicFunctionFunction);
        }

        public virtual Task<TransactionReceipt> BasicFunctionRequestAndWaitForReceiptAsync(BigInteger funcArg, CancellationTokenSource cancellationToken = null)
        {
            var basicFunctionFunction = new BasicFunctionFunction();
                basicFunctionFunction.FuncArg = funcArg;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(basicFunctionFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(BasicFunctionFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(BasicEventEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {

            };
        }
    }
}
