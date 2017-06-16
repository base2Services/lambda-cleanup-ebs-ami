using System;

namespace Base2.Lambdas.Handlers.CLI {

    public class LambdaRunner {

        public static int Main(string[] argv){
            if(argv.Length < 2){
                Console.WriteLine("usage: docker run --rm base2/lambdas $lambda $command --$arg1 value1 $arg2 value2");
                return -2;
            }

            string bucket = null, key = null, lambda = argv[0], command = argv[1];

            for(int i=0;i<argv.Length;i++){
                if(argv[i]=="--bucket"){
                    bucket = argv[i+1];
                }
                if(argv[i]=="--key"){
                    key = argv[i+1];
                }
            }
            Console.WriteLine($" lambda = {lambda}, command = {command}");
            switch(lambda){
                case "ebs-cleanup":
                    switch(command){
                        case "report":
                        break;
                        case "cleanup":
                        break;
                    }
                break;

                case "ami-cleanup":
                    switch(command){
                        case "report":
                        break;
                        case "cleanup":
                        break;
                    }
                break;
            }
            return 0;
        }
    }

}