-- Generated from the current Hibernate/JPA metadata. Validate against disposable PostgreSQL before release.
    create table attendance_records (
        date date not null,
        hours_worked float(53) not null,
        overtime_hours float(53) not null,
        clock_in timestamp(6) with time zone not null,
        clock_out timestamp(6) with time zone,
        created_at timestamp(6) with time zone,
        employee_id uuid not null,
        id uuid not null,
        device_id varchar(255),
        punch_method varchar(255) not null check (punch_method in ('Fingerprint','Manual')),
        status varchar(255) not null check (status in ('Normal','MissingClockOut','OvertimePendingApproval','OvertimeApproved')),
        primary key (id)
    );

    create table employee_profiles (
        active boolean not null,
        active_tasks_count integer not null,
        current_floor integer not null,
        hourly_rate numeric(12,2) not null,
        proficiency_level integer not null,
        tasks_completed_today integer not null,
        updated_at timestamp(6) with time zone,
        department_id uuid,
        employee_id uuid not null,
        bank_account_number varchar(255),
        bank_branch varchar(255),
        bank_name varchar(255),
        full_name varchar(255) not null,
        role varchar(255) not null check (role in ('Housekeeper','Maintenance','Chef','Waiter','Staff','Manager')),
        primary key (employee_id)
    );

    create table fnb_audit_logs (
        timestamp timestamp(6) with time zone not null,
        actor_user_id uuid,
        id uuid not null,
        order_id uuid not null,
        action varchar(255) not null,
        actor_role varchar(255),
        details TEXT,
        new_state varchar(255),
        order_number varchar(255) not null,
        previous_state varchar(255),
        reason varchar(255),
        primary key (id)
    );

    create table fnb_charge_outbox (
        retry_count integer not null,
        confirmed_at timestamp(6) with time zone,
        created_at timestamp(6) with time zone,
        next_retry_at timestamp(6) with time zone,
        finance_invoice_id uuid,
        id uuid not null,
        order_id uuid not null,
        charge_reference varchar(255) not null,
        finance_invoice_number varchar(255),
        last_error TEXT,
        payload_json TEXT not null,
        status varchar(255) not null check (status in ('Pending','Processing','Confirmed','Failed')),
        primary key (id)
    );

    create table fnb_menu_items (
        available boolean not null,
        currency varchar(3) not null,
        price numeric(12,2) not null,
        room_service_eligible boolean not null,
        id uuid not null,
        category varchar(255) not null,
        code varchar(255) not null unique,
        description TEXT,
        image_url varchar(255),
        name varchar(255) not null,
        primary key (id)
    );

    create table fnb_tax_rules (
        active boolean not null,
        tax_rate numeric(6,4) not null,
        effective_from timestamp(6) with time zone not null,
        effective_to timestamp(6) with time zone,
        id uuid not null,
        category varchar(255) not null,
        description varchar(255),
        primary key (id)
    );

    create table housekeeping_tasks (
        inspection_passed boolean,
        linen_changed boolean not null,
        cleaning_completed_at timestamp(6) with time zone,
        inspected_at timestamp(6) with time zone,
        checkout_event_id uuid,
        id uuid not null,
        inspected_by uuid,
        readiness_event_id uuid,
        inspection_notes varchar(1000),
        reclean_instructions varchar(1000),
        booking_reference varchar(255),
        cleaning_type varchar(255) not null check (cleaning_type in ('Turnover','Stayover','DeepClean','TouchUp')),
        primary key (id),
        constraint uq_housekeeping_checkout_event unique (checkout_event_id)
    );

    create table kds_orders (
        currency varchar(3),
        subtotal numeric(12,2),
        tax_amount numeric(12,2),
        total_amount numeric(12,2),
        accepted_at timestamp(6) with time zone,
        cancelled_at timestamp(6) with time zone,
        collected_at timestamp(6) with time zone,
        delivered_at timestamp(6) with time zone,
        preparing_at timestamp(6) with time zone,
        ready_at timestamp(6) with time zone,
        received_at timestamp(6) with time zone,
        booking_id uuid,
        cook_employee_id uuid,
        customer_id uuid,
        finance_invoice_id uuid,
        id uuid not null,
        waiter_employee_id uuid,
        cancellation_reason TEXT,
        charge_error TEXT,
        charge_status varchar(255) not null check (charge_status in ('PendingSubmission','PendingFinanceConfirmation','Invoiced','Failed','Waived','Cancelled')),
        chef_name varchar(255),
        customer_name varchar(255),
        finance_invoice_number varchar(255),
        items_json TEXT,
        notes TEXT,
        order_number varchar(255) not null unique,
        order_type varchar(255) not null check (order_type in ('Restaurant','RoomService')),
        room_number varchar(255),
        status varchar(255) not null check (status in ('Received','Accepted','Preparing','Ready','Collected','Delivered','Cancelled')),
        table_or_room_number varchar(255),
        waiter_name varchar(255),
        primary key (id)
    );

    create table leave_audit_logs (
        created_at timestamp(6) with time zone not null,
        actor_id uuid not null,
        id uuid not null,
        leave_request_id uuid not null,
        action varchar(40) not null,
        details varchar(1000) not null,
        primary key (id)
    );

    create table leave_policies (
        active boolean not null,
        effective_from date not null,
        effective_to date,
        paid boolean not null,
        created_at timestamp(6) with time zone not null,
        configured_by_owner_id uuid not null,
        id uuid not null,
        leave_type varchar(60) not null,
        primary key (id),
        constraint uq_leave_policy_type_effective unique (leave_type, effective_from)
    );

    create table leave_requests (
        end_date date not null,
        paid boolean not null,
        start_date date not null,
        created_at timestamp(6) with time zone not null,
        decided_at timestamp(6) with time zone,
        row_version bigint not null,
        updated_at timestamp(6) with time zone not null,
        decision_manager_id uuid,
        department_id uuid not null,
        employee_id uuid not null,
        id uuid not null,
        policy_id uuid not null,
        status varchar(20) not null check (status in ('Pending','Approved','Rejected','Cancelled')),
        leave_type varchar(60) not null,
        decision_reason varchar(1000),
        reason varchar(1000) not null,
        primary key (id)
    );

    create table maintenance_work_orders (
        actual_cost numeric(38,2),
        estimated_cost numeric(38,2),
        safety_hazard boolean not null,
        cost_approved_at timestamp(6) with time zone,
        repair_completed_at timestamp(6) with time zone,
        restriction_cleared_at timestamp(6) with time zone,
        verified_at timestamp(6) with time zone,
        clearance_event_id uuid,
        cost_approved_by uuid,
        finance_expense_id uuid,
        id uuid not null,
        issue_event_id uuid,
        restriction_event_id uuid,
        verified_by uuid,
        finance_failure varchar(1000),
        hazard_details varchar(2000),
        parts_replaced varchar(2000),
        rework_instructions varchar(2000),
        repair_notes varchar(4000),
        asset_name varchar(255) not null,
        finance_expense_status varchar(255),
        location varchar(255),
        severity varchar(255),
        primary key (id),
        constraint uq_maintenance_issue_event unique (issue_event_id)
    );

    create table monthly_attendance_summaries (
        approved_overtime_hours float(53) not null,
        approved_paid_leave_days integer not null,
        approved_unpaid_leave_days integer not null,
        attendance_disputes integer not null,
        holiday_days integer not null,
        missing_punches integer not null,
        payroll_month integer not null,
        payroll_year integer not null,
        scheduled_days integer not null,
        summary_version integer not null,
        weekend_days integer not null,
        worked_days integer not null,
        created_at timestamp(6) with time zone not null,
        updated_at timestamp(6) with time zone not null,
        verified_at timestamp(6) with time zone,
        department_id uuid not null,
        employee_id uuid not null,
        id uuid not null,
        verified_by_manager_id uuid,
        verification_status varchar(30) not null check (verification_status in ('Draft','Discrepancies','Verified','Outdated')),
        employee_role varchar(40) not null,
        attendance_record_ids text not null,
        leave_record_ids text not null,
        overtime_record_ids text not null,
        primary key (id),
        constraint uq_attendance_summary_employee_period_version unique (employee_id, payroll_year, payroll_month, summary_version)
    );

    create table overtime_approvals (
        overtime_hours float(53) not null,
        actioned_at timestamp(6) with time zone,
        requested_at timestamp(6) with time zone,
        attendance_record_id uuid not null,
        employee_id uuid not null,
        id uuid not null,
        manager_id uuid,
        manager_notes varchar(255),
        status varchar(255) not null check (status in ('Pending','Approved','Rejected')),
        primary key (id)
    );

    create table payroll_records (
        gross_pay numeric(12,2) not null,
        hourly_rate numeric(12,2) not null,
        net_pay numeric(12,2) not null,
        overtime_hours float(53) not null,
        overtime_pay numeric(12,2) not null,
        pay_period_end date not null,
        pay_period_start date not null,
        regular_hours float(53) not null,
        regular_pay numeric(12,2) not null,
        calculated_at timestamp(6) with time zone,
        employee_id uuid not null,
        id uuid not null,
        status varchar(255) not null check (status in ('Draft','Approved','Paid')),
        primary key (id)
    );

    create table staff_tasks (
        floor_number integer not null,
        rejection_count integer not null,
        accepted_at timestamp(6) with time zone,
        assigned_at timestamp(6) with time zone,
        completed_at timestamp(6) with time zone,
        created_at timestamp(6) with time zone,
        escalated_at timestamp(6) with time zone,
        started_at timestamp(6) with time zone,
        updated_at timestamp(6) with time zone,
        assigned_employee_id uuid,
        department_id uuid,
        hotel_id uuid,
        id uuid not null,
        room_id uuid,
        description varchar(2000),
        priority varchar(255) not null check (priority in ('Low','Medium','High','Urgent')),
        required_role varchar(255) not null check (required_role in ('Housekeeper','Maintenance','Chef','Waiter','Staff','Manager')),
        room_number varchar(255),
        status varchar(255) not null check (status in ('Pending','Assigned','Accepted','InProgress','Completed','AwaitingInspection','InspectionRejected','InspectionApproved','Cancelled','Escalated')),
        title varchar(255) not null,
        primary key (id)
    );

    create table task_allocation_logs (
        fairness_score float(53) not null,
        load_score float(53) not null,
        proximity_score float(53) not null,
        skill_score float(53) not null,
        total_score float(53) not null,
        allocated_at timestamp(6) with time zone,
        employee_id uuid not null,
        id uuid not null,
        task_id uuid not null,
        primary key (id)
    );

    create table task_audit_logs (
        created_at timestamp(6) with time zone not null,
        actor_id uuid not null,
        id uuid not null,
        task_id uuid not null,
        action varchar(80) not null,
        from_status varchar(80) not null,
        to_status varchar(80) not null,
        details varchar(2000),
        primary key (id)
    );

    create index idx_fnb_audit_order_id 
       on fnb_audit_logs (order_id);

    create index idx_fnb_audit_timestamp 
       on fnb_audit_logs (timestamp);

    create index idx_fnb_outbox_status_retry 
       on fnb_charge_outbox (status, next_retry_at);

    create index idx_fnb_outbox_order_id 
       on fnb_charge_outbox (order_id);

    create index idx_kds_orders_status 
       on kds_orders (status);

    create index idx_kds_orders_order_num 
       on kds_orders (order_number);

    create index ix_leave_audit_request_created 
       on leave_audit_logs (leave_request_id, created_at);

    create index ix_leave_policy_active 
       on leave_policies (leave_type, active, effective_from);

    create index ix_leave_employee_dates 
       on leave_requests (employee_id, start_date, end_date);

    create index ix_leave_department_status 
       on leave_requests (department_id, status, created_at);

    create index ix_maintenance_finance_status 
       on maintenance_work_orders (finance_expense_status, cost_approved_at);

    create index ix_attendance_summary_department_period 
       on monthly_attendance_summaries (department_id, payroll_year, payroll_month);

    create index ix_attendance_summary_status 
       on monthly_attendance_summaries (verification_status, verified_at);

    create index ix_staff_tasks_department_status 
       on staff_tasks (department_id, status);

    create index ix_staff_tasks_assignee_status 
       on staff_tasks (assigned_employee_id, status);

    create index ix_task_audit_task_created 
       on task_audit_logs (task_id, created_at);

    create index ix_task_audit_actor 
       on task_audit_logs (actor_id);

    alter table if exists housekeeping_tasks 
       add constraint FK2hjkbcpq4xiym7lowhuiuiqdd 
       foreign key (id) 
       references staff_tasks;

    alter table if exists maintenance_work_orders 
       add constraint FKbwu1qqpdxdgaqftpgdpdd3d4p 
       foreign key (id) 
       references staff_tasks;
