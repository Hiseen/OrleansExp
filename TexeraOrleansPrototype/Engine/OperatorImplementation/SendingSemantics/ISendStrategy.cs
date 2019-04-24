using Orleans;
using System.Threading.Tasks;
using Engine.WorkflowImplementation;
using System.Collections.Generic;
using Engine.OperatorImplementation.Common;
using System;
using TexeraUtilities;

namespace Engine.OperatorImplementation.SendingSemantics
{
    public interface ISendStrategy
    {
        void Enqueue(IEnumerable<TexeraTuple> output);
        void SendBatchedMessages(string senderIdentifier);
        void SendEndMessages(string senderIdentifier);
        void AddReceiver(IWorkerGrain receiver);
        void AddReceivers(List<IWorkerGrain> receivers);
    }
}