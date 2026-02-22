using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Xml.Linq;
namespace Dl
{
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
        // class lưu vết để in biểu đồ
        public class executionLog
        {
            public int startTime { get; set; }
            public int endTime { get; set; }
            public string processID { get; set; } = "";
            public string queueID { get; set; } = "";
        }
        // sjf
        public Process? getNextProcessSJF()
        {
            if (readyQueue.Count == 0) return null;
            Process shortestJob = readyQueue[0];
            foreach (Process p in readyQueue)
            {
                if (p.burstTime < shortestJob.burstTime)
                {
                    shortestJob = p;
                }
            }
            return shortestJob;
        }
    }

    public class Process
    {
        public string processID { get; set; }
        public int arrivalTime { get; set; }
        public int burstTime { get; set; }
        public int remainingTime { get; set; }
        public string queueID { get; set; }
        public bool isCompleted { get; set; } = false;
        public int completionTime { get; set; }
        public int turnaroundTime { get; set; }
        public int waitingTime { get; set; }
        public int startTime { get; set; }
        public int endTime { get; set; }

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
        public List<Queue.executionLog> logs = new List<Queue.executionLog>();
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
            logs.Add(new Queue.executionLog { startTime = start, endTime = end, queueID = qID, processID = pID });
        }
        public void SRTN()
        {
            int completedProcesses = 0;
            int currentTime = 0;
            int i = 0;
            int queueIndex = 0;
            Process? currentProcess = null;
            Queue currentQueue = queues[queueIndex];
            processes.Sort((p1, p2) => p1.arrivalTime.CompareTo(p2.arrivalTime));
            while (completedProcesses < processes.Count)
            {
                while (i < processes.Count && processes[i].arrivalTime == currentTime)
                {
                    foreach (Queue q in queues)
                    {
                        if (q.queueID == processes[i].queueID)
                        {
                            q.readyQueue.Add(processes[i]);
                            q.readyQueue.Sort((p1, p2) => p1.remainingTime.CompareTo(p2.remainingTime));
                            i++;
                            break;
                        }
                    }
                }

                if (currentQueue.remainingTime == 0)
                {
                    currentQueue.remainingTime = currentQueue.timeSlice;
                    if (currentProcess != null) currentProcess.endTime = currentTime;
                    queueIndex = (queueIndex + 1) % queues.Count;
                    currentQueue = queues[queueIndex];
                }

                if (currentQueue.readyQueue.Count == 0)
                {
                    // check tất cả hàng đợi có rỗng không
                    bool allEmpty = true;
                    foreach (var q in queues)
                    {
                        if (q.readyQueue.Count > 0) { allEmpty = false; break; }
                    }
                    if (allEmpty)
                    {
                        currentTime++; // CPU nhàn rỗi, tăng thời gian lên
                    }
                    else
                    {
                        currentQueue.remainingTime = 0; // Bỏ qua lượt của queue hiện tại
                    }
                    continue;
                }

                Process nextProcess = currentQueue.readyQueue[0];

                if (currentProcess != nextProcess)
                {
                    if (currentProcess != null) currentProcess.endTime = currentTime;
                    currentProcess = nextProcess;
                    currentProcess.startTime = currentTime;
                }

                int prevTime = currentTime;
                currentProcess.remainingTime--;
                currentQueue.remainingTime--;
                currentTime++;


                AddLog(prevTime, currentTime, currentQueue.queueID, currentProcess.processID);

                if (currentProcess.remainingTime == 0)
                {
                    currentProcess.isCompleted = true;
                    currentProcess.completionTime = currentTime;
                    currentProcess.calculateMetrics();
                    currentQueue.readyQueue.Remove(currentProcess);
                    completedProcesses++;
                }
            }
        }
        public void SJF()
        {
            int completedProcesses = 0;
            int currentTime = 0;
            int i = 0;
            Process? currentProcess = null;
            Queue currentQueue = queues[0];
            // sap xep danh sach process theo arrival time
            processes.Sort((p1, p2) => p1.arrivalTime.CompareTo(p2.arrivalTime));

            while (completedProcesses < processes.Count)
            {
                // them tat ca process da den vao ready queue
                while (i < processes.Count && processes[i].arrivalTime <= currentTime)
                {
                    currentQueue.readyQueue.Add(processes[i]);
                    i++;
                }
                // neu khong co tien trinh nao trong ready queue thi tang thoi gian
                if (currentQueue.readyQueue.Count == 0)
                {
                    currentTime++;
                    continue;
                }
                // Lay tien trinh co burst time ngan nhat
                currentProcess = currentQueue.getNextProcessSJF();
                // Check null
                if (currentProcess != null)
                {
                    currentProcess.startTime = currentTime;
                    currentTime += currentProcess.burstTime;

                    AddLog(currentProcess.startTime, currentTime, currentQueue.queueID, currentProcess.processID);

                    currentProcess.endTime = currentTime;
                    currentProcess.isCompleted = true;
                    currentProcess.completionTime = currentTime;
                    currentProcess.calculateMetrics();

                    currentQueue.readyQueue.Remove(currentProcess);
                    completedProcesses++;
                }
            }
        }
        public void RunScheduling()
        {
            int completedProcesses = 0;
            int currentTime = 0;
            int i = 0;
            int queueIndex = 0;
            Process? currentProcess = null;
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
                            // Nếu queue policy là SRTN, sắp xếp lại theo remaining time
                            if (q.schedulingPolicy == "SRTN")
                            {
                                q.readyQueue.Sort((p1, p2) => p1.remainingTime.CompareTo(p2.remainingTime));
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
                    if (currentProcess != null) currentProcess.endTime = currentTime;
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
                        currentQueue.remainingTime = 0;
                        queueIndex = (queueIndex + 1) % queues.Count;
                    }
                    continue;
                }

                // Xử lý dựa theo Policy của Queue
                if (currentQueue.schedulingPolicy == "SRTN")
                {
                    // Chuyển queue nếu queue hiện tại hết timeSlice
                    if (currentQueue.remainingTime == 0)
                    {
                        currentQueue.remainingTime = currentQueue.timeSlice;
                        if (currentProcess != null) currentProcess.endTime = currentTime;
                        queueIndex = (queueIndex + 1) % queues.Count;
                        continue;
                    }

                    Process nextProcess = currentQueue.readyQueue[0];

                    if (currentProcess != nextProcess)
                    {
                        if (currentProcess != null) currentProcess.endTime = currentTime;
                        currentProcess = nextProcess;
                        currentProcess.startTime = currentTime;
                    }
                    int prevTime = currentTime;
                    currentProcess.remainingTime--;
                    currentQueue.remainingTime--;
                    currentTime++;

                    AddLog(prevTime, currentTime, currentQueue.queueID, currentProcess.processID);
                }
                else if (currentQueue.schedulingPolicy == "SJF")
                {
                    currentProcess = currentQueue.getNextProcessSJF();
                    if (currentProcess != null)
                    {

                        int executionTime = Math.Min(currentProcess.remainingTime, currentQueue.remainingTime);
                        currentProcess.startTime = currentTime;
                        currentTime += executionTime; // SJF nhảy cóc thời gian
                        currentProcess.remainingTime -= executionTime;
                        currentQueue.remainingTime -= executionTime;

                        AddLog(currentProcess.startTime, currentTime, currentQueue.queueID, currentProcess.processID);
                    }
                }
                if (currentProcess != null && currentProcess.remainingTime == 0)
                {
                    currentProcess.isCompleted = true;
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
            processes.Sort((a, b) => a.processID.CompareTo(b.processID));

            double totalWT = 0, totalTT = 0;
            foreach (var p in processes)
            {
                Console.WriteLine("{0,-10}{1,-10}{2,-10}{3,-12}{4,-12}{5,-10}",
                    p.processID, p.arrivalTime, p.burstTime, p.completionTime, p.turnaroundTime, p.waitingTime);

                totalWT += p.waitingTime;
                totalTT += p.turnaroundTime;
            }

            int count = processes.Count;
            Console.WriteLine("--------------------------------------------------------------");
            Console.WriteLine($"Average Turnaround Time: {totalTT / count:F1}"); // In 1 chữ số thập phân [cite: 111]
            Console.WriteLine($"Average Waiting Time:    {totalWT / count:F1}"); // [cite: 112]

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }

    class ReadFile
    {
        public void readDataFromFile(string fileName, SchedulingResult simulator)
        {
            if (!File.Exists(fileName))
            {
                Console.WriteLine("File not found!");
                return;
            }

            string[] lines = File.ReadAllLines(fileName);
            int i = 0;
            int qCount = int.Parse(lines[i].Trim());
            i++;
            for (int j = 0; j < qCount; j++)
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
                simulator.processes.Sort((a, b) => a.processID.CompareTo(b.processID));

                double totalWT = 0, totalTT = 0;
                foreach (var p in simulator.processes)
                {
                    writer.WriteLine("{0,-10}{1,-10}{2,-10}{3,-12}{4,-12}{5,-10}",
                        p.processID, p.arrivalTime, p.burstTime, p.completionTime, p.turnaroundTime, p.waitingTime);

                    totalWT += p.waitingTime;
                    totalTT += p.turnaroundTime;
                }

                int count = simulator.processes.Count;
                writer.WriteLine("--------------------------------------------------------------");
                writer.WriteLine($"Average Turnaround Time: {totalTT / count:F1}");
                writer.WriteLine($"Average Waiting Time:    {totalWT / count:F1}");
            }
        }
    }
    class Program
    {
        static void Main(string[] args)
        {
            // 1. Khởi tạo danh sách rỗng để chuẩn bị nhận dữ liệu từ file
            List<Process> pList = new List<Process>();
            List<Queue> qList = new List<Queue>();

            // 2. Tạo đối tượng Simulator (SchedulingResult)
            // Lưu ý: Đảm bảo constructor của SchedulingResult nhận (List<Process>, List<Queue>)
            SchedulingResult simulator = new SchedulingResult(pList, qList);

            // 3. Đọc dữ liệu từ file input.txt
            ReadFile reader = new ReadFile();
            string inputFileName = "input.txt"; // Hoặc lấy từ args[0] theo yêu cầu Lab 

            Console.WriteLine($"--- Reading data from file: {inputFileName} ---");
            reader.readDataFromFile(inputFileName, simulator);

            // Kiểm tra nếu dữ liệu rỗng
            if (simulator.processes.Count == 0 || simulator.queues.Count == 0)
            {
                Console.WriteLine("No processes or queues found in the input file. Please check the file format.");
                return;
            }

            // 4. Thực thi thuật toán điều phối
            simulator.RunScheduling(); // Gọi hàm SRTN đã fix logic chuyển queue của bạn
            simulator.PrintSchedulingDiagram(); // Gọi hàm in biểu đồ
            reader.ExportResultsToFile("output.txt", simulator); // Xuất kết quả ra file output.txt
        }
    }
}
