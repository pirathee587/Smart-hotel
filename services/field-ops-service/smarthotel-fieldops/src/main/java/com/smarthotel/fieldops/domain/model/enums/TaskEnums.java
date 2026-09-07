package com.smarthotel.fieldops.domain.model.enums;

public class TaskEnums {

    public enum TaskRole {
        Housekeeper,
        Maintenance,
        Chef,
        Waiter,
        Staff,
        Manager
    }

    public enum TaskPriority {
        Low,
        Medium,
        High,
        Urgent
    }

    public enum TaskStatus {
        Pending,
        Assigned,
        InProgress,
        Completed,
        Cancelled,
        Escalated
    }

    public enum CleaningType {
        Turnover,
        Stayover,
        DeepClean,
        TouchUp
    }

    public enum KdsStatus {
        Received,
        Preparing,
        Ready,
        Delivered,
        Cancelled
    }

    public enum AttendanceStatus {
        Normal,
        MissingClockOut,
        OvertimePendingApproval,
        OvertimeApproved
    }

    public enum PunchMethod {
        Fingerprint,
        Manual
    }

    public enum OvertimeStatus {
        Pending,
        Approved,
        Rejected
    }

    public enum PayrollStatus {
        Draft,
        Approved,
        Paid
    }
}
