using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Xml.Linq;
namespace Lab01
{
    public class executionLog
    {
        public int startTime { get; set; }
        public int endTime { get; set; }
        public string processID { get; set; } = "";
        public string queueID { get; set; } = "";
    }
    public class Queue
    {
        public string queueID;
        public int timeSlice;
        public string schedulingPolicy;
        public int remainingTime;
        public List<Process> readyQueue = new List<Process>();
        public Queue(string id, int ts, string sp)
        {
            queueID = id;
            timeSlice = ts;
            schedulingPolicy = sp;
            remainingTime = ts;
        }
    }

    public class Process
    {
        public string processID { get; set; }
        public int arrivalTime { get; set; }
        public int burstTime { get; set; }
        public int remainingTime { get; set; }
        public string queueID { get; set; }
        public int completionTime { get; set; }
        public int turnaroundTime { get; set; }
        public int waitingTime { get; set; }

        public Process(string pid, int at, int bt, string qid)
        {
            processID = pid;
            arrivalTime = at;
            burstTime = bt;
            remainingTime = bt;
            queueID = qid;
        }
        public void calculateMetrics()
        {
            turnaroundTime = completionTime - arrivalTime;
            waitingTime = turnaroundTime - burstTime;
        }
    }
    public class SchedulingResult
    {
        public List<Process> processes { get; set; }
        public List<Queue> queues { get; set; }
        public List<executionLog> logs = new List<executionLog>();
        public SchedulingResult(List<Process> p, List<Queue> q)
        {
            processes = p;
            queues = q;
        }
        // gộp các mốc thời gian của cùng một process (VD: [36-39] và [39-40] thành [36-40])
        private void AddLog(int start, int end, string qID, string pID)
        {
            if (start == end) return;
            if (logs.Count > 0)
            {
                var lastLog = logs[logs.Count - 1];
                if (lastLog.endTime == start && lastLog.queueID == qID && lastLog.processID == pID)
                {
                    lastLog.endTime = end;
                    return;
                }
            }
            logs.Add(new executionLog { startTime = start, endTime = end, queueID = qID, processID = pID });
        }

        public void RunScheduling()
        {
            int completedProcesses = 0;
            int currentTime = 0;
            int i = 0;
            int queueIndex = 0;
            Process? currentProcess = null;

            queues.Sort((q1,q2) => q2.timeSlice.CompareTo(q1.timeSlice)); // Sắp xếp các queue theo time slice giảm dần để ưu tiên queue có time slice lớn hơn
            processes.Sort((p1, p2) => p1.arrivalTime.CompareTo(p2.arrivalTime));

            while (completedProcesses < processes.Count)
            {
                // Thêm tiến trình vào các hàng đợi tương ứng khi đến giờ
                while (i < processes.Count && processes[i].arrivalTime <= currentTime)
                {
                    foreach (Queue q in queues)
                    {
                        if (q.queueID == processes[i].queueID)
                        {
                            q.readyQueue.Add(processes[i]);
                            
                            if (q.schedulingPolicy == "SRTN")
                            {
                                q.readyQueue.Sort((p1, p2) => p1.remainingTime.CompareTo(p2.remainingTime)); // Nếu queue policy là SRTN, sắp xếp lại theo remaining time
                            }
                            else if (q.schedulingPolicy == "SJF")
                            {
                                q.readyQueue.Sort((p1, p2) => p1.burstTime.CompareTo(p2.burstTime)); // Nếu queue policy là SJF, sắp xếp lại theo burst time
                            }
                            break;
                        }
                    }
                    i++;
                }

                Queue currentQueue = queues[queueIndex];
                if (currentQueue.remainingTime == 0)
                {
                    currentQueue.remainingTime = currentQueue.timeSlice;
                    queueIndex = (queueIndex + 1) % queues.Count;
                    currentQueue = queues[queueIndex];
                }

                // Nếu queue hiện tại rỗng, kiểm tra xem có nên tăng thời gian hay chuyển queue
                if (currentQueue.readyQueue.Count == 0)
                {
                    bool allEmpty = true;
                    foreach (var q in queues)
                    {
                        if (q.readyQueue.Count > 0) { allEmpty = false; break; }
                    }
                    if (allEmpty)
                    {
                        currentTime++; // Tất cả queue đều rỗng, tăng thời gian lên chờ process mới
                    }
                    else
                    {
                        // Chuyển sang queue tiếp theo (bỏ qua queue rỗng)
                        currentQueue.remainingTime = currentQueue.timeSlice; // Reset thời gian của queue mới
                        queueIndex = (queueIndex + 1) % queues.Count;
                    }
                    continue;
                }

                int prevTime = currentTime;
                currentProcess = currentQueue.readyQueue[0];
                int executionTime = Math.Min(currentProcess.remainingTime, currentQueue.remainingTime);
                // Xử lý dựa theo Policy của Queue
                if (currentQueue.schedulingPolicy == "SJF")
                {
                    currentTime += executionTime; // SJF nhảy cóc thời gian
                    currentProcess.remainingTime -= executionTime;
                    currentQueue.remainingTime -= executionTime;
                }
                else if (currentQueue.schedulingPolicy == "SRTN")
                {
                    int timeToNextArrival = int.MaxValue;
                    if (i < processes.Count) 
                    {
                        timeToNextArrival = processes[i].arrivalTime - currentTime;
                    }
                    
                    executionTime = Math.Min(executionTime, timeToNextArrival); 

                    currentTime += executionTime; // SRTN nhảy cóc thời gian
                    currentProcess.remainingTime -= executionTime;
                    currentQueue.remainingTime -= executionTime;
                }

                AddLog(prevTime, currentTime, currentQueue.queueID, currentProcess.processID);

                if (currentProcess != null && currentProcess.remainingTime == 0)
                {
                    currentProcess.completionTime = currentTime;
                    currentProcess.calculateMetrics();
                    currentQueue.readyQueue.Remove(currentProcess);
                    completedProcesses++;
                }
            }
        }
        public void PrintSchedulingDiagram()
        {
            Console.WriteLine("=================== CPU SCHEDULING DIAGRAM ===================\n");
            Console.WriteLine("{0,-18} {1,-10} {2,-10}", "[Start - End]", "Queue", "Process");
            Console.WriteLine("-----------------------------------------");
            foreach (var log in logs)
            {
                Console.WriteLine("{0,-18} {1,-10} {2,-10}", $"[{log.startTime} - {log.endTime}]", log.queueID, log.processID);
            }
            
            Console.WriteLine("\n");
            Console.WriteLine("===================== PROCESS STATISTICS =====================\n");
            Console.WriteLine("--------------------------------------------------------------");
            Console.WriteLine("{0,-10}{1,-10}{2,-10}{3,-12}{4,-12}{5,-10}",
                "Process", "Arrival", "Burst", "Completion", "Turnaround", "Waiting");
            Console.WriteLine("--------------------------------------------------------------");
            

            double totalWT = 0, totalTT = 0;
            foreach (var p in processes)
            {
                Console.WriteLine("{0,-10}{1,-10}{2,-10}{3,-12}{4,-12}{5,-10}",
                    p.processID, p.arrivalTime, p.burstTime, p.completionTime, p.turnaroundTime, p.waitingTime);

                totalWT += p.waitingTime;
                totalTT += p.turnaroundTime;
            }

            Console.WriteLine("--------------------------------------------------------------");
            Console.WriteLine($"Average Turnaround Time: {totalTT / processes.Count:F1}");
            Console.WriteLine($"Average Waiting Time:    {totalWT / processes.Count:F1}");
            Console.WriteLine("==============================================================");
        }
    }

    class FileHandler
    {
        public void ReadDataFromFile(string fileName, SchedulingResult simulator)
        {
            if (!File.Exists(fileName))
            {
                Console.WriteLine($"File {fileName} not found!");
                return;
            }

            string[] lines = File.ReadAllLines(fileName);
            int i = 0;
            int qCount = int.Parse(lines[i].Trim());
            i++;

            while (i < qCount + 1)
            {
                string[] queueData = lines[i].Trim().Split(' ');
                string queueID = queueData[0];
                int timeSlice = int.Parse(queueData[1]);
                string schedulingPolicy = queueData[2];
                Queue q = new Queue(queueID, timeSlice, schedulingPolicy);
                simulator.queues.Add(q);
                i++;
            }

            while (i < lines.Length)
            {
                string[] processData = lines[i].Trim().Split(' ');
                string processID = processData[0];
                int arrivalTime = int.Parse(processData[1]);
                int burstTime = int.Parse(processData[2]);
                string queueID = processData[3];
                Process p = new Process(processID, arrivalTime, burstTime, queueID);
                simulator.processes.Add(p);
                i++;
            }
        }
        public void ExportResultsToFile(string fileName, SchedulingResult simulator)
        {
            using (StreamWriter writer = new StreamWriter(fileName))
            {
                writer.WriteLine("=================== CPU SCHEDULING DIAGRAM ===================\n");
                writer.WriteLine("{0,-18} {1,-10} {2,-10}", "[Start - End]", "Queue", "Process");
                writer.WriteLine("-----------------------------------------");
                foreach (var log in simulator.logs)
                {
                    writer.WriteLine("{0,-18} {1,-10} {2,-10}", $"[{log.startTime} - {log.endTime}]", log.queueID, log.processID);
                }

                writer.WriteLine("\n");
                writer.WriteLine("===================== PROCESS STATISTICS =====================\n");
                writer.WriteLine("--------------------------------------------------------------");
                writer.WriteLine("{0,-10}{1,-10}{2,-10}{3,-12}{4,-12}{5,-10}",
                    "Process", "Arrival", "Burst", "Completion", "Turnaround", "Waiting");
                writer.WriteLine("--------------------------------------------------------------");

                double totalWT = 0, totalTT = 0;
                foreach (var p in simulator.processes)
                {
                    writer.WriteLine("{0,-10}{1,-10}{2,-10}{3,-12}{4,-12}{5,-10}",
                        p.processID, p.arrivalTime, p.burstTime, p.completionTime, p.turnaroundTime, p.waitingTime);

                    totalWT += p.waitingTime;
                    totalTT += p.turnaroundTime;
                }

                writer.WriteLine("--------------------------------------------------------------");
                writer.WriteLine($"Average Turnaround Time: {totalTT / simulator.processes.Count:F1}");
                writer.WriteLine($"Average Waiting Time:    {totalWT / simulator.processes.Count:F1}");
                writer.WriteLine("==============================================================");
            }
        }
    }
    class Program
    {
        static void Main(string[] args)
        {
            List<Process> pList = new List<Process>();
            List<Queue> qList = new List<Queue>();

            SchedulingResult simulator = new SchedulingResult(pList, qList);

            FileHandler fHandler = new FileHandler();
            string inputFileName = "input.txt";
            string outputFileName = "output.txt";

            Console.WriteLine($"--- Reading data from file: {inputFileName} ---");
            fHandler.ReadDataFromFile(inputFileName, simulator);
            if (simulator.processes.Count == 0 || simulator.queues.Count == 0)
            {
                Console.WriteLine("No processes or queues found in the input file. Please check the file format.");
                return;
            }

            simulator.RunScheduling();
            simulator.PrintSchedulingDiagram();

            fHandler.ExportResultsToFile(outputFileName, simulator);
        }
    }
}
