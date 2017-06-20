using System;
namespace Base2.Lambdas.Models
{
    public class EBSCleanupInput : S3Input
    {
        public EBSCleanupInput()
        {
        }

		private int _startIndex = 0;

		public int StartIndex
		{
			get
			{
				return _startIndex;
			}
			set
			{
				_startIndex = value;
			}
		}

	}
}
