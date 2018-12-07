using Orleans;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Engine.OperatorImplementation.Interfaces
{
    public interface IScanOperator : IGrainWithIntegerKey
    {
        Task SubmitTuples();

        Task LoadTuples();
    }
}