namespace FWO.Basics
{
    public class LexicographicalListComparer : IComparer<List<int>>
    {
        public int Compare(List<int> x, List<int> y)
        {
            int minLen = Math.Min(x.Count, y.Count);

            for (int i = 0; i < minLen; i++)
            {
                int cmp = x[i].CompareTo(y[i]);
                if (cmp != 0) return cmp;
            }
            
            return x.Count.CompareTo(y.Count);
        }
    }

}