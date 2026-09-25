package com.smarthotel.fieldops.service.allocation;

import com.smarthotel.fieldops.domain.model.EmployeeProfile;
import com.smarthotel.fieldops.domain.model.StaffTask;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskPriority;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskRole;
import com.smarthotel.fieldops.domain.model.enums.TaskEnums.TaskStatus;
import com.smarthotel.fieldops.domain.repository.EmployeeProfileRepository;
import com.smarthotel.fieldops.domain.repository.AttendanceRecordRepository;
import com.smarthotel.fieldops.domain.repository.StaffTaskRepository;
import com.smarthotel.fieldops.domain.repository.TaskAllocationLogRepository;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.util.*;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.within;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
class AllocationEngineTest {

    @Mock
    private EmployeeProfileRepository employeeProfileRepository;

    @Mock
    private StaffTaskRepository staffTaskRepository;

    @Mock
    private TaskAllocationLogRepository allocationLogRepository;

    @Mock
    private AttendanceRecordRepository attendanceRecordRepository;

    private AllocationEngine allocationEngine;

    @BeforeEach
    void setUp() {
        // Concrete configuration: maxConcurrentTasks = 5, maxDailyTasks = 10
        allocationEngine = new AllocationEngine(
                employeeProfileRepository,
                staffTaskRepository,
                allocationLogRepository,
                attendanceRecordRepository,
                5,
                10
        );
    }

    @Test
    @DisplayName("Formula weights verify TotalScore = 0.35×skill + 0.25×proximity + 0.25×load + 0.15×fairness")
    void verifyMathematicalWeights() {
        // Perfect score on all components: 1.0, 1.0, 1.0, 1.0
        double perfectScore = allocationEngine.calculateTotalScore(1.0, 1.0, 1.0, 1.0);
        assertThat(perfectScore).isCloseTo(1.0, within(0.0001));

        // Skill only
        double skillOnly = allocationEngine.calculateTotalScore(1.0, 0.0, 0.0, 0.0);
        assertThat(skillOnly).isCloseTo(0.35, within(0.0001));

        // Proximity only
        double proxOnly = allocationEngine.calculateTotalScore(0.0, 1.0, 0.0, 0.0);
        assertThat(proxOnly).isCloseTo(0.25, within(0.0001));

        // Load only
        double loadOnly = allocationEngine.calculateTotalScore(0.0, 0.0, 1.0, 0.0);
        assertThat(loadOnly).isCloseTo(0.25, within(0.0001));

        // Fairness only
        double fairnessOnly = allocationEngine.calculateTotalScore(0.0, 0.0, 0.0, 1.0);
        assertThat(fairnessOnly).isCloseTo(0.15, within(0.0001));
    }

    @Test
    @DisplayName("Evaluate Candidate computes correct subscores based on original linear-decay formulas")
    void evaluateCandidate_ComputesCorrectSubscores_LinearFormulas() {
        StaffTask task = StaffTask.builder()
                .id(UUID.randomUUID())
                .title("Clean Room 302")
                .requiredRole(TaskRole.Housekeeper)
                .floorNumber(3)
                .build();

        // Proficiency = 5 -> S_skill = 5 / 5.0 = 1.0
        // CurrentFloor = 3, TaskFloor = 3 -> diff 0 -> S_proximity = max(1 - 0*0.2, 0) = 1.0
        // ActiveTasks = 1, MaxConcurrent = 5 -> S_load = 1 - min(1/5, 1.0) = 0.8
        // CompletedToday = 3, MaxDaily = 10 -> S_fairness = 1 - min(3/10, 1.0) = 0.7
        EmployeeProfile emp = EmployeeProfile.builder()
                .employeeId(UUID.randomUUID())
                .fullName("Kamal Perera")
                .role(TaskRole.Housekeeper)
                .proficiencyLevel(5)
                .currentFloor(3)
                .activeTasksCount(1)
                .tasksCompletedToday(3)
                .build();

        AllocationEngine.CandidateScore score = allocationEngine.evaluateCandidate(emp, task);

        assertThat(score.skillScore()).isCloseTo(1.0, within(0.0001));
        assertThat(score.proximityScore()).isCloseTo(1.0, within(0.0001));
        assertThat(score.loadScore()).isCloseTo(0.8, within(0.0001));
        assertThat(score.fairnessScore()).isCloseTo(0.7, within(0.0001));

        // Multi-signal score also includes neutral defaults for unavailable history signals.
        assertThat(score.totalScore()).isCloseTo(0.75475, within(0.0001));
    }

    @Test
    @DisplayName("Proximity decay: max(1 - |taskFloor - employeeFloor| × 0.2, 0.0)")
    void evaluateCandidate_ProximityDecaysLinearly() {
        StaffTask taskFloor5 = StaffTask.builder()
                .id(UUID.randomUUID())
                .requiredRole(TaskRole.Housekeeper)
                .floorNumber(5)
                .build();

        // Diff 1: floor 4 -> 1 - 1*0.2 = 0.8
        EmployeeProfile empFloor4 = EmployeeProfile.builder()
                .role(TaskRole.Housekeeper)
                .currentFloor(4)
                .build();
        assertThat(allocationEngine.evaluateCandidate(empFloor4, taskFloor5).proximityScore())
                .isCloseTo(0.8, within(0.0001));

        // Diff 2: floor 3 -> 1 - 2*0.2 = 0.6
        EmployeeProfile empFloor3 = EmployeeProfile.builder()
                .role(TaskRole.Housekeeper)
                .currentFloor(3)
                .build();
        assertThat(allocationEngine.evaluateCandidate(empFloor3, taskFloor5).proximityScore())
                .isCloseTo(0.6, within(0.0001));

        // Diff 5: floor 0 -> 1 - 5*0.2 = 0.0
        EmployeeProfile empFloor0 = EmployeeProfile.builder()
                .role(TaskRole.Housekeeper)
                .currentFloor(0)
                .build();
        assertThat(allocationEngine.evaluateCandidate(empFloor0, taskFloor5).proximityScore())
                .isCloseTo(0.0, within(0.0001));

        // Diff 7: floor -2 -> clamped to 0.0
        EmployeeProfile empFar = EmployeeProfile.builder()
                .role(TaskRole.Housekeeper)
                .currentFloor(12)
                .build();
        assertThat(allocationEngine.evaluateCandidate(empFar, taskFloor5).proximityScore())
                .isCloseTo(0.0, within(0.0001));
    }

    @Test
    @DisplayName("Skill score: ProficiencyLevel / 5 (1.0 if no skill required, 0.2 fallback)")
    void evaluateCandidate_SkillScoreLinearAndFallback() {
        StaffTask maintenanceTask = StaffTask.builder()
                .requiredRole(TaskRole.Maintenance)
                .floorNumber(1)
                .build();

        // Matching role with proficiency level 4 -> 4 / 5.0 = 0.8
        EmployeeProfile empProf4 = EmployeeProfile.builder()
                .role(TaskRole.Maintenance)
                .proficiencyLevel(4)
                .currentFloor(1)
                .build();
        assertThat(allocationEngine.evaluateCandidate(empProf4, maintenanceTask).skillScore())
                .isCloseTo(0.8, within(0.0001));

        // Non-matching role -> 0.2 fallback
        EmployeeProfile empHousekeeper = EmployeeProfile.builder()
                .role(TaskRole.Housekeeper)
                .proficiencyLevel(5)
                .currentFloor(1)
                .build();
        assertThat(allocationEngine.evaluateCandidate(empHousekeeper, maintenanceTask).skillScore())
                .isCloseTo(0.2, within(0.0001));

        // Task with no specific skill required (Staff) -> 1.0
        StaffTask generalTask = StaffTask.builder()
                .requiredRole(TaskRole.Staff)
                .floorNumber(1)
                .build();
        assertThat(allocationEngine.evaluateCandidate(empHousekeeper, generalTask).skillScore())
                .isCloseTo(1.0, within(0.0001));
    }

    @Test
    @DisplayName("Load and Fairness linear decay with saturation clamping to 0.0")
    void evaluateCandidate_LoadAndFairnessSaturation() {
        StaffTask task = StaffTask.builder()
                .requiredRole(TaskRole.Housekeeper)
                .floorNumber(1)
                .build();

        // MaxConcurrentTasks = 5, activeTasks = 5 -> S_load = 1 - 5/5 = 0.0
        // MaxDailyTasks = 10, completedToday = 10 -> S_fairness = 1 - 10/10 = 0.0
        EmployeeProfile empFull = EmployeeProfile.builder()
                .role(TaskRole.Housekeeper)
                .currentFloor(1)
                .activeTasksCount(5)
                .tasksCompletedToday(10)
                .build();

        AllocationEngine.CandidateScore scoreFull = allocationEngine.evaluateCandidate(empFull, task);
        assertThat(scoreFull.loadScore()).isCloseTo(0.0, within(0.0001));
        assertThat(scoreFull.fairnessScore()).isCloseTo(0.0, within(0.0001));

        // Beyond max -> clamped to 0.0
        EmployeeProfile empOverloaded = EmployeeProfile.builder()
                .role(TaskRole.Housekeeper)
                .currentFloor(1)
                .activeTasksCount(8)
                .tasksCompletedToday(15)
                .build();

        AllocationEngine.CandidateScore scoreOver = allocationEngine.evaluateCandidate(empOverloaded, task);
        assertThat(scoreOver.loadScore()).isCloseTo(0.0, within(0.0001));
        assertThat(scoreOver.fairnessScore()).isCloseTo(0.0, within(0.0001));
    }

    @Test
    @DisplayName("AllocateTask assigns task to the highest-scoring candidate under linear formulas")
    void allocateTask_AssignsToHighestScoringCandidate() {
        UUID departmentId = UUID.randomUUID();
        StaffTask task = StaffTask.builder()
                .id(UUID.randomUUID())
                .title("Turnover Cleaning 204")
                .requiredRole(TaskRole.Housekeeper)
                .priority(TaskPriority.High)
                .floorNumber(2)
                .status(TaskStatus.Pending)
                .departmentId(departmentId)
                .build();

        // Candidate A: On floor 2 (prox 1.0), activeTasks = 2 (load 1 - 2/5 = 0.6), completedToday = 4 (fairness 1 - 4/10 = 0.6)
        // Score A: 0.35(1.0) + 0.25(1.0) + 0.25(0.6) + 0.15(0.6) = 0.35 + 0.25 + 0.15 + 0.09 = 0.84
        EmployeeProfile empA = EmployeeProfile.builder()
                .employeeId(UUID.randomUUID())
                .fullName("Staff A")
                .role(TaskRole.Housekeeper)
                .departmentId(departmentId)
                .proficiencyLevel(5)
                .currentFloor(2)
                .activeTasksCount(2)
                .tasksCompletedToday(4)
                .build();

        // Candidate B: On floor 3 (prox 1 - 1*0.2 = 0.8), activeTasks = 0 (load 1.0), completedToday = 0 (fairness 1.0)
        // Score B: 0.35(1.0) + 0.25(0.8) + 0.25(1.0) + 0.15(1.0) = 0.35 + 0.20 + 0.25 + 0.15 = 0.95
        EmployeeProfile empB = EmployeeProfile.builder()
                .employeeId(UUID.randomUUID())
                .fullName("Staff B")
                .role(TaskRole.Housekeeper)
                .departmentId(departmentId)
                .proficiencyLevel(5)
                .currentFloor(3)
                .activeTasksCount(0)
                .tasksCompletedToday(0)
                .build();

        when(employeeProfileRepository.findByRoleAndDepartmentIdAndActiveTrue(TaskRole.Housekeeper, departmentId))
                .thenReturn(List.of(empA, empB));

        Optional<EmployeeProfile> allocated = allocationEngine.allocateTask(task, Collections.emptySet());

        assertThat(allocated).isPresent();
        assertThat(allocated.get().getEmployeeId()).isEqualTo(empB.getEmployeeId());
        assertThat(task.getAssignedEmployeeId()).isEqualTo(empB.getEmployeeId());
        assertThat(task.getStatus()).isEqualTo(TaskStatus.Assigned);
        assertThat(empB.getActiveTasksCount()).isEqualTo(1);

        verify(staffTaskRepository).save(task);
        verify(employeeProfileRepository).save(empB);
        verify(allocationLogRepository, times(2)).save(any());
    }

    @Test
    @DisplayName("Recommendations rank best available employee first and exclude fully loaded staff")
    void rankAvailableCandidates_ExcludesUnavailableAndRanksBestFirst() {
        UUID departmentId = UUID.randomUUID();
        StaffTask task = StaffTask.builder()
                .id(UUID.randomUUID()).requiredRole(TaskRole.Housekeeper)
                .departmentId(departmentId).floorNumber(2).build();
        EmployeeProfile available = EmployeeProfile.builder()
                .employeeId(UUID.randomUUID()).fullName("Available")
                .role(TaskRole.Housekeeper).departmentId(departmentId)
                .activeTasksCount(1).currentFloor(2).proficiencyLevel(5).build();
        EmployeeProfile busy = EmployeeProfile.builder()
                .employeeId(UUID.randomUUID()).fullName("Fully loaded")
                .role(TaskRole.Housekeeper).departmentId(departmentId)
                .activeTasksCount(5).currentFloor(2).proficiencyLevel(5).build();
        when(employeeProfileRepository.findByRoleAndDepartmentIdAndActiveTrue(TaskRole.Housekeeper, departmentId))
                .thenReturn(List.of(busy, available));

        List<AllocationEngine.CandidateScore> ranked = allocationEngine.rankAvailableCandidates(task, Set.of());

        assertThat(ranked).extracting(score -> score.employee().getEmployeeId())
                .containsExactly(available.getEmployeeId());
    }
}
