using System;
namespace Base2.Lambdas.Models
{
    public class AMICleanupInput : S3Input
    {
        public AMICleanupInput()
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
