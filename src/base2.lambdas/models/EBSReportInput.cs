using System;

namespace Base2.Lambdas.Models {

    public class EBSReportInput {

        public string BucketName {get;set;}

        public string Key {get;set;}

        private bool _OnlyAmiOrphans = true;

       
        public bool OnlyAMIOrphans {
            get {
                return _OnlyAmiOrphans;
            }
            set {
                _OnlyAmiOrphans = value;
            }
        }
    }

}