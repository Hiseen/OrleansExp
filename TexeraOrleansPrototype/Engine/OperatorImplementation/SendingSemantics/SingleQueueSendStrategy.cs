using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Engine.OperatorImplementation.Common;
using Orleans;
using Orleans.Concurrency;
using Orleans.Core;
using TexeraUtilities;

namespace Engine.OperatorImplementation.SendingSemantics
{
    public abstract class SingleQueueSendStrategy: SingleQueueBatching, ISendStrategy
    {
        protected List<FlowControlUnit> receivers;
        protected List<ulong> outputSequenceNumbers;
        public SingleQueueSendStrategy(int batchingLimit=1000):base(batchingLimit)
        {
            this.receivers=new List<FlowControlUnit>();
            this.outputSequenceNumbers=new List<ulong>();
        }

        public void AddReceiver(IWorkerGrain receiver)
        {
            receivers.Add(new FlowControlUnit(receiver));
            this.outputSequenceNumbers.Add(0);
        }

        public void AddReceivers(List<IWorkerGrain> receivers)
        {
            foreach(IWorkerGrain grain in receivers)
            {
                this.receivers.Add(new FlowControlUnit(grain));
            }
            this.outputSequenceNumbers.AddRange(Enumerable.Repeat((ulong)0,receivers.Count));
        }

        public void RemoveAllReceivers()
        {
            receivers.Clear();
            this.outputSequenceNumbers.Clear();
        }

        public abstract void SendBatchedMessages(IGrain senderIdentifier);

        public abstract void SendEndMessages(IGrain senderIdentifier);

    }
}