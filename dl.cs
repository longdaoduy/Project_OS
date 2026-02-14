using System;
using System.Data;
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
        public int turnAroundTime { get; set; }
        public int waitingTime { get; set; }

        public Process(string pid, int at, int bt, string qid)
        {
            processID = pid;
            arrivalTime = at;
            burstTime = bt;
            remainingTime = bt;
            queueID = qid;
        }
    }
}