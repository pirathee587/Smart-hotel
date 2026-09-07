package com.smarthotel.fieldops.service.allocation;

import com.smarthotel.fieldops.domain.model.EmployeeProfile;
import com.smarthotel.fieldops.domain.model.StaffTask;
import com.smarthotel.fieldops.domain.model.TaskAllocationLog;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskRole;
import com.smarthotel.fieldops.domain.repository.EmployeeProfileRepository;
import com.smarthotel.fieldops.domain.repository.StaffTaskRepository;
import com.smarthotel.fieldops.domain.repository.TaskAllocationLogRepository;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.Instant;
import java.util.*;

@Service
@Slf4j
public class AllocationEngine {

    // Exact blueprint weights: TotalScore = 0.35×skill + 0.25×proximity + 0.25×load + 0.15×fairness
    public static final double WEIGHT_SKILL = 0.35;
    public static final double WEIGHT_PROXIMITY = 0.25;
    public static final double WEIGHT_LOAD = 0.25;
    public static final double WEIGHT_FAIRNESS = 0.15;

    private final EmployeeProfileRepository employeeProfileRepository;
    private final StaffTaskRepository staffTaskRepository;
    private final TaskAllocationLogRepository allocationLogRepository;

    private final int maxConcurrentTasks;
    private final int maxDailyTasks;

    public AllocationEngine(
            EmployeeProfileRepository employeeProfileRepository,
            StaffTaskRepository staffTaskRepository,
            TaskAllocationLogRepository allocationLogRepository,
            @Value("${allocation.max-concurrent-tasks:5}") int maxConcurrentTasks,
            @Value("${allocation.max-daily-tasks:10}") int maxDailyTasks) {
        this.employeeProfileRepository = employeeProfileRepository;
        this.staffTaskRepository = staffTaskRepository;
        this.allocationLogRepository = allocationLogRepository;
        this.maxConcurrentTasks = maxConcurrentTasks > 0 ? maxConcurrentTasks : 5;
        this.maxDailyTasks = maxDailyTasks > 0 ? maxDailyTasks : 10;
    }

    public int getMaxConcurrentTasks() {
        return maxConcurrentTasks;
    }

    public int getMaxDailyTasks() {
        return maxDailyTasks;
    }

    public record CandidateScore(
            EmployeeProfile employee,
            double skillScore,
            double proximityScore,
            double loadScore,
            double fairnessScore,
            double totalScore) {}

    public double calculateTotalScore(double skill, double proximity, double load, double fairness) {
        return (WEIGHT_SKILL * skill)
                + (WEIGHT_PROXIMITY * proximity)
                + (WEIGHT_LOAD * load)
                + (WEIGHT_FAIRNESS * fairness);
    }

    public CandidateScore evaluateCandidate(EmployeeProfile employee, StaffTask task) {
        // 1. Skill Score: S_skill = ProficiencyLevel / 5 (1.0 if no skill required, 0.2 fallback)
        double skillScore;
        if (task.getRequiredRole() == null || task.getRequiredRole() == TaskRole.Staff) {
            skillScore = 1.0;
        } else if (employee.getRole() == task.getRequiredRole()) {
            int proficiency = employee.getProficiencyLevel() > 0 ? employee.getProficiencyLevel() : 5;
            skillScore = Math.min(1.0, Math.max(0.2, (double) proficiency / 5.0));
        } else {
            skillScore = 0.2; // fallback
        }

        // 2. Proximity Score: S_proximity = max(1 - |taskFloor - employeeFloor| × 0.2, 0.0)
        int floorDiff = Math.abs(task.getFloorNumber() - employee.getCurrentFloor());
        double proximityScore = Math.max(1.0 - (floorDiff * 0.2), 0.0);

        // 3. Load Score: S_load = 1 - min(activeTasks / MaxConcurrentTasks, 1.0)
        double loadRatio = (double) employee.getActiveTasksCount() / this.maxConcurrentTasks;
        double loadScore = 1.0 - Math.min(loadRatio, 1.0);

        // 4. Fairness Score: S_fairness = 1 - min(tasksCompletedToday / MaxDailyTasks, 1.0)
        double fairnessRatio = (double) employee.getTasksCompletedToday() / this.maxDailyTasks;
        double fairnessScore = 1.0 - Math.min(fairnessRatio, 1.0);

        double totalScore = calculateTotalScore(skillScore, proximityScore, loadScore, fairnessScore);

        return new CandidateScore(employee, skillScore, proximityScore, loadScore, fairnessScore, totalScore);
    }

    @Transactional
    public Optional<EmployeeProfile> allocateTask(StaffTask task, Set<UUID> excludedEmployeeIds) {
        log.info("Running Weighted Allocation Engine for task {} (Role: {}, Priority: {}, Floor: {})",
                task.getId(), task.getRequiredRole(), task.getPriority(), task.getFloorNumber());

        List<EmployeeProfile> eligibleEmployees = employeeProfileRepository.findByRoleAndActiveTrue(task.getRequiredRole());
        if (eligibleEmployees.isEmpty()) {
            eligibleEmployees = employeeProfileRepository.findByRoleAndActiveTrue(TaskRole.Staff);
        }

        if (eligibleEmployees.isEmpty()) {
            log.warn("No eligible active employees available for role {}", task.getRequiredRole());
            return Optional.empty();
        }

        List<CandidateScore> scoredCandidates = new ArrayList<>();

        for (EmployeeProfile candidate : eligibleEmployees) {
            if (excludedEmployeeIds != null && excludedEmployeeIds.contains(candidate.getEmployeeId())) {
                continue; // Skip employee who previously rejected this task
            }

            CandidateScore score = evaluateCandidate(candidate, task);
            scoredCandidates.add(score);

            // Persist allocation evaluation log
            TaskAllocationLog logEntry = TaskAllocationLog.builder()
                    .taskId(task.getId())
                    .employeeId(candidate.getEmployeeId())
                    .skillScore(score.skillScore())
                    .proximityScore(score.proximityScore())
                    .loadScore(score.loadScore())
                    .fairnessScore(score.fairnessScore())
                    .totalScore(score.totalScore())
                    .allocatedAt(Instant.now())
                    .build();
            allocationLogRepository.save(logEntry);
        }

        if (scoredCandidates.isEmpty()) {
            log.warn("All eligible candidates have been excluded or unavailable for task {}", task.getId());
            return Optional.empty();
        }

        // Sort descending by totalScore
        scoredCandidates.sort((c1, c2) -> Double.compare(c2.totalScore(), c1.totalScore()));
        CandidateScore winner = scoredCandidates.getFirst();

        EmployeeProfile selectedEmployee = winner.employee();
        task.assignTo(selectedEmployee.getEmployeeId());
        selectedEmployee.incrementActiveTasks();

        staffTaskRepository.save(task);
        employeeProfileRepository.save(selectedEmployee);

        log.info("Task {} allocated to employee {} with score {}",
                task.getId(), selectedEmployee.getFullName(), winner.totalScore());

        return Optional.of(selectedEmployee);
    }
}
