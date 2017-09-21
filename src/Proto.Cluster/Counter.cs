namespace Proto.Cluster
{
	public class Counter
	{
		private int val;

		public int Next()
		{
			return val++;
		}
	}
}