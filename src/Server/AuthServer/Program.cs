using System;
using Common;

namespace AuthServer
{
    class Program
    {
        static void Main(string[] args)
        {
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
