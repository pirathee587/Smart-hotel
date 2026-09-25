import api from "@/lib/axios";
export type MaintenanceStatus="Pending"|"Assigned"|"Accepted"|"InProgress"|"AwaitingInspection"|"InspectionRejected"|"InspectionApproved"|"Cancelled"|"Escalated";
export interface MaintenanceTask{id:string;title:string;description?:string;status:MaintenanceStatus;priority:string;assignedEmployeeId?:string;departmentId:string;roomId?:string;roomNumber?:string;assetName?:string;location?:string;severity?:string;safetyHazard?:boolean;hazardDetails?:string;estimatedCost?:number;actualCost?:number;partsReplaced?:string;repairNotes?:string;reworkInstructions?:string;financeExpenseId?:string;financeExpenseStatus?:string;restrictionClearedAt?:string;}
export interface MaintenanceProfile{employeeId:string;fullName:string;role:string;departmentId:string;}
export interface TaskCandidate{employeeId:string;fullName:string;role:string;activeTasksCount:number;currentFloor:number;proficiencyLevel:number;attendanceScore:number;shiftAvailabilityScore:number;qualityScore:number;completionSpeedScore:number;guestRatingScore:number;rejectionScore:number;totalScore:number;recommended:boolean;}
const root="/api/v1/tasks";
export const maintenanceApi={
 list:async(manager:boolean)=>(await api.get<MaintenanceTask[]>(manager?root:`${root}/my`)).data.filter(x=>x.assetName||x.title.toLowerCase().includes("maintenance")),
 profiles:async()=>(await api.get<MaintenanceProfile[]>(`${root}/profiles`)).data.filter(x=>x.role==="Maintenance"),
 recommendations:async(id:string)=>(await api.get<TaskCandidate[]>(`${root}/${id}/recommendations`)).data,
 create:async(payload:{title:string;description:string;roomId:string;roomNumber:string;assetName:string;severity:string;safetyHazard:boolean;hazardDetails:string;estimatedCost:number})=>(await api.post<MaintenanceTask>(`${root}/maintenance`,{...payload,issueEventId:crypto.randomUUID(),location:`Room ${payload.roomNumber}`,priority:payload.severity==="Critical"?"Urgent":payload.severity==="High"?"High":"Medium",autoDispatch:false})).data,
 assign:async(id:string,employeeId:string)=>(await api.post<MaintenanceTask>(`${root}/${id}/assign`,{employeeId})).data,
 accept:async(id:string)=>(await api.post<MaintenanceTask>(`${root}/${id}/accept`)).data,
 start:async(id:string)=>(await api.post<MaintenanceTask>(`${root}/${id}/start-repair`)).data,
 complete:async(id:string,notes:string,actualCost:number,partsReplaced:string)=>(await api.post<MaintenanceTask>(`${root}/${id}/complete`,{notes,actualCost,partsReplaced})).data,
 verify:async(id:string,approved:boolean,notes:string)=>(await api.post<MaintenanceTask>(`${root}/${id}/maintenance-verification`,{approved,notes})).data,
 approveCost:async(id:string)=>(await api.post<MaintenanceTask>(`${root}/${id}/maintenance-cost-approval`,{currency:"LKR"})).data,
};
