using Engine.OperatorImplementation.Common;
using Orleans;

namespace Engine.OperatorImplementation.Operators
{
    public class GroupByOperator : Operator
    {
        
        public GroupByOperator(KeywordPredicate predicate) : base(predicate)
        {
            
        }

        public override void SetPrincipalGrain(IGrainFactory factory)
        {
            PrincipalGrain = factory.GetGrain<IGroupByPrincipalGrain>(OperatorGuid);
        }
    }
}