using System;
using Common;
using static Common.Errors;

namespace AuthServer
{
    class Program
    {
        static void Main(string[] args)
        {
            //Assert(false);
            Banner.Show("AuthServer", "FelCore 0.1.0",
                (s) => {
                    Console.WriteLine(s);
                },
                () => {
                    Console.WriteLine("Some extra info!");
                }
            );
        }
    }
}
