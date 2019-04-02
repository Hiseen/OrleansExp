using Engine.OperatorImplementation.Common;

namespace Engine.OperatorImplementation.Operators
{
    public class KeywordPredicate : PredicateBase
    {
        public int SearchIndex;
        public string Query;

        public KeywordPredicate(int searchIndex, string query)
        {
            if(query == null)
            {
                query = "";
            }
            this.Query = query;
            this.SearchIndex=searchIndex;
        }
    }
}