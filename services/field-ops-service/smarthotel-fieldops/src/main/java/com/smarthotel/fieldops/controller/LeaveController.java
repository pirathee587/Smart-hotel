package com.smarthotel.fieldops.controller;
import com.smarthotel.fieldops.domain.model.*; import com.smarthotel.fieldops.dto.LeaveDtos.*; import com.smarthotel.fieldops.service.LeaveService; import jakarta.validation.Valid; import lombok.RequiredArgsConstructor; import org.springframework.http.*; import org.springframework.security.access.prepost.PreAuthorize; import org.springframework.security.core.annotation.AuthenticationPrincipal; import org.springframework.security.oauth2.jwt.Jwt; import org.springframework.web.bind.annotation.*; import java.util.*;
@RestController @RequestMapping("/api/v1/leaves") @RequiredArgsConstructor
public class LeaveController {
 private final LeaveService service;
 @GetMapping("/policies") @PreAuthorize("isAuthenticated()") public List<PolicyResponse> policies(){return service.activePolicies().stream().map(this::policy).toList();}
 @PostMapping("/policies") @PreAuthorize("hasRole('Owner')") public ResponseEntity<PolicyResponse> configure(@Valid @RequestBody PolicyRequest input,@AuthenticationPrincipal Jwt jwt){return ResponseEntity.status(201).body(policy(service.configure(user(jwt),input)));}
 @PostMapping @PreAuthorize("isAuthenticated()") public ResponseEntity<LeaveResponse> submit(@Valid @RequestBody SubmitRequest input,@AuthenticationPrincipal Jwt jwt){return ResponseEntity.status(201).body(map(service.submit(user(jwt),input)));}
 @GetMapping("/my") @PreAuthorize("isAuthenticated()") public List<LeaveResponse> mine(@AuthenticationPrincipal Jwt jwt){return service.mine(user(jwt)).stream().map(this::map).toList();}
 @PostMapping("/{id}/cancel") @PreAuthorize("isAuthenticated()") public LeaveResponse cancel(@PathVariable UUID id,@AuthenticationPrincipal Jwt jwt){return map(service.cancel(id,user(jwt)));}
 @GetMapping("/department") @PreAuthorize("hasAnyRole('Owner', 'Manager')") public List<LeaveResponse> department(@RequestParam(required = false) UUID departmentId, @AuthenticationPrincipal Jwt jwt){
     if (isOwner(jwt)) {
         if (departmentId != null) return service.department(departmentId).stream().map(this::map).toList();
         return service.allLeaves().stream().map(this::map).toList();
     }
     return service.department(departmentId(jwt)).stream().map(this::map).toList();
 }
 @PostMapping("/{id}/decision") @PreAuthorize("hasRole('Manager')") public LeaveResponse decide(@PathVariable UUID id,@Valid @RequestBody DecisionRequest input,@AuthenticationPrincipal Jwt jwt){return map(service.decide(id,user(jwt),departmentId(jwt),input));}
 private UUID user(Jwt jwt){return UUID.fromString(jwt.getSubject());} private UUID departmentId(Jwt jwt){Object v=jwt.getClaims().get("departmentId");if(v==null)throw new SecurityException("Department assignment required.");return UUID.fromString(v.toString());}
 private boolean isOwner(Jwt jwt){Object r=jwt!=null?jwt.getClaims().get("role"):null;return r!=null&&"Owner".equals(r.toString());}
 private LeaveResponse map(LeaveRequest l){return new LeaveResponse(l.getId(),l.getEmployeeId(),l.getDepartmentId(),l.getPolicyId(),l.getLeaveType(),l.isPaid(),l.getStartDate(),l.getEndDate(),l.getReason(),l.getStatus(),l.getDecisionManagerId(),l.getDecisionReason(),l.getDecidedAt(),l.getCreatedAt(),l.getRowVersion());}
 private PolicyResponse policy(LeavePolicy p){return new PolicyResponse(p.getId(),p.getLeaveType(),p.isPaid(),p.getEffectiveFrom(),p.getEffectiveTo(),p.isActive());}
}
