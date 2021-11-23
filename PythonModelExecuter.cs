using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    public class PythonModelExecuter
    {
        private static Process _process;
        private readonly Dispatcher _dispatcher;

        public event Action<string> OutputRedirected;
        public PythonModelExecuter(FileInfo pythonFile, Dispatcher dispatcher)
        {
            //TODO : 지금 한 명 당 파이썬 프로세스 하나라 무거운가..? 풀링 사용하기..?
            var psi = new ProcessStartInfo
            {
                FileName = @"C:\Users\raki2\anaconda3\envs\torch_tutorial\python.exe",
                Arguments = $"-u \"{pythonFile.FullName}\"",

                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
            };

            _process = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true,
            };

            bool launchSuccess = _process.Start();

            if (!launchSuccess)
            {
                Console.WriteLine("Failed to launch process");
                return;
            }
            _dispatcher = dispatcher;

            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    string result = _process.StandardOutput.ReadLine();

                    if (!string.IsNullOrEmpty(result))
                    {
                        //OutputRedirected?.Invoke(result);
                        _dispatcher.Invoke(OutputRedirected, result);
                    }
                }
            });
        }

        public void ExecuteModel(string clipDirectory)
        {
            _process.StandardInput.WriteLine(clipDirectory);
            _process.StandardInput.Flush();
        }
    }
}
