import api from "@/lib/axios";

export type HousekeepingStatus = "Pending"|"Assigned"|"Accepted"|"InProgress"|"AwaitingInspection"|"InspectionRejected"|"InspectionApproved"|"Cancelled"|"Escalated";
export interface HousekeepingTask { id:string; title:string; description?:string; requiredRole:string; priority:string; status:HousekeepingStatus; assignedEmployeeId?:string; floorNumber:number; departmentId:string; roomId?:string; roomNumber?:string; bookingReference?:string; createdAt:string; assignedAt?:string; acceptedAt?:string; startedAt?:string; cleaningCompletedAt?:string; inspectedBy?:string; inspectedAt?:string; inspectionPassed?:boolean; inspectionNotes?:string; recleanInstructions?:string; }
export interface HousekeeperProfile { employeeId:string; fullName:string; role:string; departmentId:string; activeTasksCount:number; }
export interface TaskCandidate { employeeId:string; fullName:string; role:string; activeTasksCount:number; currentFloor:number; proficiencyLevel:number; attendanceScore:number; shiftAvailabilityScore:number; qualityScore:number; completionSpeedScore:number; guestRatingScore:number; rejectionScore:number; totalScore:number; recommended:boolean; }

const root="/api/v1/tasks";
export const housekeepingApi={
 list:async(manager:boolean)=>(await api.get<HousekeepingTask[]>(manager?root:`${root}/my`)).data.filter(t=>t.requiredRole==="Housekeeper"),
 profiles:async()=>(await api.get<HousekeeperProfile[]>(`${root}/profiles`)).data.filter(p=>p.role==="Housekeeper"),
 recommendations:async(taskId:string)=>(await api.get<TaskCandidate[]>(`${root}/${taskId}/recommendations`)).data,
 assign:async(taskId:string,employeeId:string)=>(await api.post<HousekeepingTask>(`${root}/${taskId}/assign`,{employeeId})).data,
 accept:async(taskId:string)=>(await api.post<HousekeepingTask>(`${root}/${taskId}/accept`)).data,
 start:async(taskId:string)=>(await api.post<HousekeepingTask>(`${root}/${taskId}/start-cleaning`)).data,
 complete:async(taskId:string)=>(await api.post<HousekeepingTask>(`${root}/${taskId}/complete`)).data,
 inspect:async(taskId:string,approved:boolean,notes:string)=>(await api.post<HousekeepingTask>(`${root}/${taskId}/inspection`,{approved,notes})).data,
};
