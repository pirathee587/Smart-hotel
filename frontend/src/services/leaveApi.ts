import api from "@/lib/axios";
export interface LeavePolicy { id:string; leaveType:string; paid:boolean; effectiveFrom:string; effectiveTo?:string; active:boolean }
export interface LeaveRequest { id:string; employeeId:string; departmentId:string; policyId:string; leaveType:string; paid:boolean; startDate:string; endDate:string; reason:string; status:"Pending"|"Approved"|"Rejected"|"Cancelled"; decisionManagerId?:string; decisionReason?:string; decidedAt?:string; createdAt:string; rowVersion:number }
const root="/api/v1/leaves";
export const leaveApi={
 policies:async()=>(await api.get<LeavePolicy[]>(`${root}/policies`)).data,
 configurePolicy:async(payload:{leaveType:string;paid:boolean;effectiveFrom:string;effectiveTo?:string})=>(await api.post<LeavePolicy>(`${root}/policies`,payload)).data,
 mine:async()=>(await api.get<LeaveRequest[]>(`${root}/my`)).data,
 department:async()=>(await api.get<LeaveRequest[]>(`${root}/department`)).data,
 submit:async(payload:{policyId:string;startDate:string;endDate:string;reason:string})=>(await api.post<LeaveRequest>(root,payload)).data,
 decide:async(id:string,approve:boolean,reason:string)=>(await api.post<LeaveRequest>(`${root}/${id}/decision`,{approve,reason})).data,
 cancel:async(id:string)=>(await api.post<LeaveRequest>(`${root}/${id}/cancel`)).data,
};
