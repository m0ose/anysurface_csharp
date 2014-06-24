/*
 * Copyright 2014. Redfish Group LLC
 * 
 * Camera server 
 * 
 * 
 * 
 * 
 * 
 * */
// REDFISH GROUP MAKES NO REPRESENTATIONS OR WARRANTIES ABOUT THE SUITABILITY OF THE
// SOFTWARE, EITHER EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
// IMPLIED WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE, OR NON-INFRINGEMENT. REDFISH GROUP SHALL NOT BE LIABLE FOR ANY DAMAGES
// SUFFERED BY LICENSEE AS A RESULT OF USING, MODIFYING OR DISTRIBUTING
// THIS SOFTWARE OR ITS DERIVATIVES.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Threading;
using System.Diagnostics;
using serverAgent;

namespace AnySurfaceWebServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Server serv = null;
            try
            {
                serv = new Server(8080, "fart");
                serv.startServer();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return;
            }

            var ck = new ConsoleKeyInfo();

            do//main loop
            {
                Console.WriteLine(" Press escape (esc) to exit");
                ck = Console.ReadKey();
            } while (ck.Key != ConsoleKey.Escape);

            serv.stopCamera();
        }

    }
}
