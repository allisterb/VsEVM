using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts;
using System.Threading;

namespace TestNS.BasicContract.ContractDefinition
{


    public partial class BasicContractDeployment : BasicContractDeploymentBase
    {
        public BasicContractDeployment() : base(BYTECODE) { }
        public BasicContractDeployment(string byteCode) : base(byteCode) { }
    }

    public class BasicContractDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "6080604052348015600f57600080fd5b506101598061001f6000396000f3fe6080604052600436106100225760003560e01c806392b4bb8a1461002e57610029565b3661002957005b600080fd5b34801561003a57600080fd5b50610055600480360381019061005091906100cc565b610057565b005b7fb532177e7c269fcc7b98812c0fc27f2dd1970ad68f68cea8c18f0191848377a2816040516100869190610108565b60405180910390a150565b600080fd5b6000819050919050565b6100a981610096565b81146100b457600080fd5b50565b6000813590506100c6816100a0565b92915050565b6000602082840312156100e2576100e1610091565b5b60006100f0848285016100b7565b91505092915050565b61010281610096565b82525050565b600060208201905061011d60008301846100f9565b9291505056fea26469706673582212208a392ee8312943b2a99577ccac7ddae2d81e2cf186c3cf0146671a20ee5c803364736f6c634300081b0033";
        public BasicContractDeploymentBase() : base(BYTECODE) { }
        public BasicContractDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class BasicFunctionFunction : BasicFunctionFunctionBase { }

    [Function("basicFunction")]
    public class BasicFunctionFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "funcArg", 1)]
        public virtual BigInteger FuncArg { get; set; }
    }

    public partial class BasicEventEventDTO : BasicEventEventDTOBase { }

    [Event("BasicEvent")]
    public class BasicEventEventDTOBase : IEventDTO
    {
        [Parameter("uint256", "eventArg", 1, false )]
        public virtual BigInteger EventArg { get; set; }
    }


}
