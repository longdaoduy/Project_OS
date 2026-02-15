using System;
using System.Collections.Generic;

using System.Data;
using System.Xml.Linq;
namespace Dl
{
    public class Queue
    {
        public string queueID;
        public int timeSlice;
        public string schedulingPolicy;
        public List<Process> readyQueue = new List<Process>();
        public Queue(string id, int ts, string sp)
        {
            queueID = id;
            timeSlice = ts;
            schedulingPolicy = sp;
        }
        // sjf
        public Process getNextProcessSJF()
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
    }
    public class SchedulingResult
    {
        public List<Process> processes { get; set; }
        public Queue queue { get; set; }
        public SchedulingResult(List<Process> p, Queue q)
        {
            processes = p;
            queue = q;
        }
        public void SRTN()
        {
            int completedProcesses = 0;
            int currentTime = 0;
            int i = 0;
            Process currentProcess = null;
            while (completedProcesses < processes.Count)
            {
                if (currentTime == 0)
                {
                    queue.readyQueue.Add(processes[i]);
                    i++;
                }
                else if (i < processes.Count && processes[i].arrivalTime == currentTime)
                {
                    queue.readyQueue.Add(processes[i]);
                    queue.readyQueue.Sort((p1, p2) => p1.burstTime.CompareTo(p2.burstTime));
                    i++;
                }

                Process nextProcess = queue.readyQueue[0];

                if (currentProcess != nextProcess)
                {
                    if (currentProcess != null)
                    {
                        currentProcess.endTime = currentTime;
                    }
                    currentProcess = nextProcess;
                    currentProcess.startTime = currentTime;
                }

                currentProcess.remainingTime--;
                currentTime++;

                if (currentProcess.remainingTime == 0)
                {
                    currentProcess.isCompleted = true;
                    currentProcess.completionTime = currentTime;
                    currentProcess.turnaroundTime = currentTime - currentProcess.arrivalTime;
                    currentProcess.waitingTime = currentProcess.turnaroundTime - currentProcess.burstTime;
                    queue.readyQueue.Remove(currentProcess);
                    completedProcesses++;
                }
            }
        }
        public void SJF()
        {
            int completedProcesses = 0;
            int currentTime = 0;
            int i = 0;
            Process currentProcess = null;
            // sap xep danh sach process theo arrival time
            processes.Sort((p1, p2) => p1.arrivalTime.CompareTo(p2.arrivalTime));

            while (completedProcesses < processes.Count)
            {
                // them tat ca process da den vao ready queue
                while (i < processes.Count && processes[i].arrivalTime <= currentTime)
                {
                    queue.readyQueue.Add(processes[i]);
                    i++;
                }
                // neu khong co tien trinh nao trong ready queue thi tang thoi gian
                if (queue.readyQueue.Count == 0)
                {
                    currentTime++;
                    continue;
                }
                // Lay tien trinh co burst time ngan nhat
                currentProcess = queue.getNextProcessSJF();
                currentProcess.startTime = currentTime;
                currentTime += currentProcess.burstTime;

                // cap nhat thong tin cho tien trinh da hoan thanh
                currentProcess.endTime = currentTime;
                currentProcess.isCompleted = true;
                currentProcess.completionTime = currentTime;
                currentProcess.turnaroundTime = currentProcess.completionTime - currentProcess.arrivalTime;
                currentProcess.waitingTime = currentProcess.turnaroundTime - currentProcess.burstTime;

                // Xoa tien trinh da hoan thanh khoi ready queue va tang bien dem
                queue.readyQueue.Remove(currentProcess);
                completedProcesses++;
            }
        }
    }
    class Program
    {
        static void Main(string[] args)
        {
            // Nạp dữ liệu mô phỏng từ tài liệu: P1(0,24), P2(1,5), P3(2,3)
            List<Process> pList = new List<Process>
            {
                new Process("P1", 0, 24, "Q1"),
                new Process("P2", 1, 5, "Q1"),
                new Process("P3", 2, 3, "Q1")
            };

            Queue q = new Queue("Q1", 0, "SRTN");
            SchedulingResult simulator = new SchedulingResult(pList, q);

            simulator.SRTN();

            // In bảng kết quả
            Console.WriteLine("Process\tArrival\tBurst\tTT\tWT");
            Console.WriteLine("-------------------------------------------");

            // Sắp xếp lại theo ID để in cho đẹp
            pList.Sort((a, b) => a.processID.CompareTo(b.processID));

            double totalWT = 0, totalTT = 0;
            foreach (var p in pList)
            {
                Console.WriteLine($"{p.processID}\t{p.arrivalTime}\t{p.burstTime}\t{p.turnaroundTime}\t{p.waitingTime}");
                totalWT += p.waitingTime;
                totalTT += p.turnaroundTime;
            }

            Console.WriteLine("-------------------------------------------");
            Console.WriteLine($"AVG Turnaround Time: {totalTT / pList.Count:F2}");
            Console.WriteLine($"AVG Waiting Time: {totalWT / pList.Count:F2}");

            Console.ReadLine();
        }

        public void caculateMetrics()
        {
            turnaroundTime = completionTime - arrivalTime;
            waitingTime = turnaroundTime - burstTime;
        }
    }
}
