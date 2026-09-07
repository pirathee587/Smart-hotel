"use client";

import React, { useEffect, useState } from "react";
import { Card } from "@/components/ui/Card";
import { Badge } from "@/components/ui/Badge";
import api from "@/lib/axios";
import { CheckSquare, User, RefreshCw, CheckCircle, AlertCircle } from "lucide-react";


interface StaffTask {
  id: string;
  title: string;
  requiredRole: string; // Housekeeper, Maintenance, Admin, etc.
  targetFloor: number;
  priority: number; // 0=Low, 1=Medium, 2=High
  status: number; // 0=Pending, 1=Assigned, 2=Accepted, 3=InProgress, 4=Completed, 5=Cancelled
  assignedEmployeeName?: string;
}

export default function TasksPage() {
  const [tasks, setTasks] = useState<StaffTask[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  const fetchTasks = async () => {
    setIsLoading(true);
    try {
      const res = await api.get<StaffTask[]>("/api/v1/tasks");
      setTasks(res.data);
    } catch {
      // Fallback demo tasks
      setTasks([
        {
          id: "t1",
          title: "Clean Suite 301",
          requiredRole: "Housekeeper",
          targetFloor: 3,
          priority: 2,
          status: 0,
        },
        {
          id: "t2",
          title: "Fix leaky pipe in room 102",
          requiredRole: "Maintenance",
          targetFloor: 1,
          priority: 1,
          status: 3,
          assignedEmployeeName: "John Doe",
        },
        {
          id: "t3",
          title: "Setup extra bed in room 201",
          requiredRole: "Housekeeper",
          targetFloor: 2,
          priority: 0,
          status: 4,
          assignedEmployeeName: "Jane Smith",
        },
      ]);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchTasks();
  }, []);

  const handleAction = async (taskId: string, action: string) => {
    try {
      await api.post(`/api/v1/tasks/${taskId}/${action}`);
      fetchTasks();
    } catch {
      // Local fallback status updates for UI demonstration
      setTasks((prev) =>
        prev.map((t) => {
          if (t.id === taskId) {
            let nextStatus = t.status;
            if (action === "accept") nextStatus = 2;
            else if (action === "start") nextStatus = 3;
            else if (action === "complete") nextStatus = 4;
            else if (action === "reject") nextStatus = 0;
            return { ...t, status: nextStatus };
          }
          return t;
        })
      );
    }
  };

  const getPriorityBadge = (prio: number) => {
    switch (prio) {
      case 2:
        return <Badge variant="danger">High</Badge>;
      case 1:
        return <Badge variant="warning">Medium</Badge>;
      default:
        return <Badge variant="info">Low</Badge>;
    }
  };

  const getStatusBadge = (status: number) => {
    switch (status) {
      case 0:
        return <Badge variant="danger">Pending</Badge>;
      case 1:
        return <Badge variant="info">Assigned</Badge>;
      case 2:
        return <Badge variant="info">Accepted</Badge>;
      case 3:
        return <Badge variant="warning">In Progress</Badge>;
      case 4:
        return <Badge variant="success">Completed</Badge>;
      case 5:
        return <Badge variant="ghost">Cancelled</Badge>;
      default:
        return <Badge variant="ghost">Unknown</Badge>;
    }
  };

  return (
    <div className="space-y-8">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold tracking-tight bg-gradient-to-r from-text-primary to-text-secondary bg-clip-text text-transparent">
            Staff Task Allocation
          </h1>
          <p className="text-sm text-text-secondary mt-1">
            Assign and monitor tasks for housekeeping and maintenance crews.
          </p>
        </div>
        <button
          onClick={fetchTasks}
          className="flex items-center gap-2 px-4 py-2 bg-white/5 border border-card-border hover:bg-white/10 rounded-lg text-sm font-medium transition-all cursor-pointer"
        >
          <RefreshCw className="w-4 h-4" /> Refresh
        </button>
      </div>

      {/* Task List container */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {isLoading ? (
          <div className="col-span-full py-12 text-center text-text-secondary">
            Loading tasks...
          </div>
        ) : tasks.length === 0 ? (
          <div className="col-span-full py-12 text-center text-text-secondary">
            No active tasks found.
          </div>
        ) : (
          tasks.map((task) => (
            <Card key={task.id} className="flex flex-col justify-between border border-card-border/80 p-6 min-h-[200px]">
              <div>
                <div className="flex items-start justify-between">
                  <div className="flex items-center gap-3">
                    <div className="w-8 h-8 rounded-lg bg-primary/10 border border-primary/20 flex items-center justify-center text-primary">
                      <CheckSquare className="w-4 h-4" />
                    </div>
                    <div>
                      <h3 className="font-semibold text-text-primary text-base">
                        {task.title}
                      </h3>
                      <span className="text-xs text-text-secondary">
                        Floor {task.targetFloor} • Role: {task.requiredRole}
                      </span>
                    </div>
                  </div>
                  <div className="flex flex-col items-end gap-1.5">
                    {getStatusBadge(task.status)}
                    {getPriorityBadge(task.priority)}
                  </div>
                </div>

                {task.assignedEmployeeName && (
                  <div className="mt-4 flex items-center gap-2 text-xs text-text-secondary">
                    <User className="w-3.5 h-3.5 text-primary" />
                    <span>Assigned to: <strong className="text-text-primary">{task.assignedEmployeeName}</strong></span>
                  </div>
                )}
              </div>

              {/* Task Actions */}
              <div className="flex gap-2 mt-6 pt-4 border-t border-card-border/50">
                {task.status === 0 && (
                  <button
                    onClick={() => handleAction(task.id, "accept")}
                    className="flex-1 py-2 bg-primary hover:bg-primary-hover text-white text-xs font-semibold rounded-lg cursor-pointer transition-all text-center"
                  >
                    Accept Task
                  </button>
                )}
                {task.status === 2 && (
                  <button
                    onClick={() => handleAction(task.id, "start")}
                    className="flex-1 py-2 bg-amber-500 hover:bg-amber-600 text-black text-xs font-semibold rounded-lg cursor-pointer transition-all text-center"
                  >
                    Start Progress
                  </button>
                )}
                {task.status === 3 && (
                  <div className="flex gap-2 w-full">
                    <button
                      onClick={() => handleAction(task.id, "complete")}
                      className="flex-1 py-2 bg-emerald-500 hover:bg-emerald-600 text-white text-xs font-semibold rounded-lg cursor-pointer transition-all text-center"
                    >
                      Complete Task
                    </button>
                    <button
                      onClick={() => handleAction(task.id, "reject")}
                      className="flex-1 py-2 bg-accent/10 border border-accent/20 hover:bg-accent text-accent text-xs font-semibold rounded-lg cursor-pointer transition-all text-center"
                    >
                      Decline / Reject
                    </button>
                  </div>
                )}
                {task.status === 4 && (
                  <div className="text-xs text-emerald-400 italic text-center w-full flex items-center justify-center gap-1.5">
                    <CheckCircle className="w-4 h-4" /> Task Completed Successfully
                  </div>
                )}
              </div>
            </Card>
          ))
        )}
      </div>
    </div>
  );
}
