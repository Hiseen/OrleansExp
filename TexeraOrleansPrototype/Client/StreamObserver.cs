using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans.Streams;
using Orleans.Concurrency;
using System.Diagnostics;
using TexeraUtilities;
using Engine;

namespace OrleansClient
{
    public class StreamObserver : IAsyncObserver<Immutable<PayloadMessage>>
    {
        public List<TexeraTuple> resultsToRet = new List<TexeraTuple>();
        Stopwatch sw=new Stopwatch();
        private Dictionary<string,ulong> currentSequenceNumber=new Dictionary<string, ulong>();
        private Dictionary<string,Dictionary<ulong,Immutable<PayloadMessage>>> stashedMessage=new Dictionary<string, Dictionary<ulong, Immutable<PayloadMessage>>>();
        private int numEndFlags;
        private int currentEndFlags=0;
        public bool isFinished=false;
        public Task Start()
        {
            sw.Start();
            return Task.CompletedTask;
        }

        public void SetNumEndFlags(int num)
        {
            numEndFlags=num;
        }

        public Task OnCompletedAsync()
        {
            Console.WriteLine("Chatroom message stream received stream completed event");
            return Task.CompletedTask;
        }

        public Task OnErrorAsync(Exception ex)
        {
            Console.WriteLine($"Chatroom is experiencing message delivery failure, ex :{ex}");
            return Task.CompletedTask;
        }

        public Task OnNextAsync(Immutable<PayloadMessage> item, StreamSequenceToken token = null)
        {
            string sender=item.Value.SenderIdentifer;
            ulong seqNum=item.Value.SequenceNumber;
            bool isEnd=item.Value.IsEnd;
            if(!currentSequenceNumber.ContainsKey(sender))
            {
                currentSequenceNumber.Add(sender,0);
            }
            if(currentSequenceNumber[sender]<seqNum)
            {
                //ahead
                stashedMessage[sender][seqNum]=item;
            }
            else if(currentSequenceNumber[sender]>seqNum)
            {
                //duplicate
                return Task.CompletedTask;
            }
            else
            {
                List<TexeraTuple> currentPayload=item.Value.Payload;
                while(true)
                {
                    if(currentPayload!=null)
                    {
                        resultsToRet.AddRange(currentPayload);
                    }
                    ulong nextSeqNum=currentSequenceNumber[sender]++;
                    if(stashedMessage.ContainsKey(sender) && stashedMessage[sender].ContainsKey(nextSeqNum))
                    {
                        Immutable<PayloadMessage> message=stashedMessage[sender][nextSeqNum];
                        currentPayload=message.Value.Payload;
                        isEnd|=message.Value.IsEnd;
                        stashedMessage[sender].Remove(nextSeqNum);
                    }
                    else
                        break;
                }
            }

            if(isEnd)
            {
                currentEndFlags++;
            }

            if(currentEndFlags==numEndFlags)
            {
                isFinished=true;
                sw.Stop();
                Console.WriteLine("Time usage: " + sw.Elapsed);
            }
            return Task.CompletedTask;
        }
    }
}