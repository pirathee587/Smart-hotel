import { z } from "zod";

/**
 * Zod schemas for Dashboard API response runtime validation.
 * Mirrors RoomDto, DashboardBookingDto, and StaffTaskDto field-for-field.
 */

export const roomSchema = z.object({
  id: z.string(),
  roomNumber: z.string(),
  floor: z.number(),
  status: z.number(),
  roomTypeName: z.string(),
});

export type RoomSchemaType = z.infer<typeof roomSchema>;
export const roomsListSchema = z.array(roomSchema);

export const dashboardBookingSchema = z.object({
  id: z.string(),
  roomId: z.string(),
  bookingReference: z.string().optional(),
  customerName: z.string().optional(),
  customerLastName: z.string().optional(),
  customerEmail: z.string().optional(),
  roomNumber: z.string(),
  checkInDate: z.string(),
  checkOutDate: z.string(),
  status: z.union([z.number(), z.string()]),
  totalAmount: z.number(),
  paymentReference: z.string().optional(),
  payHereOrderId: z.string().optional(),
});

export type DashboardBookingSchemaType = z.infer<typeof dashboardBookingSchema>;
export const dashboardBookingsListSchema = z.array(dashboardBookingSchema);

export const staffTaskSchema = z.object({
  id: z.string(),
  title: z.string(),
  requiredRole: z.string(),
  targetFloor: z.number(),
  priority: z.number(),
  status: z.number(),
  assignedEmployeeName: z.string().optional(),
  skillMatch: z.number().optional(),
  proximity: z.number().optional(),
  load: z.number().optional(),
  fairness: z.number().optional(),
});

export type StaffTaskSchemaType = z.infer<typeof staffTaskSchema>;
export const staffTasksListSchema = z.array(staffTaskSchema);

// ── Floor View ───────────────────────────────────────────────────────────────

export const roomCategoryGroupSchema = z.object({
  roomTypeId: z.string(),
  roomTypeName: z.string(),
  rooms: z.array(roomSchema),
});

export const floorViewSchema = z.object({
  floor: z.number(),
  totalRooms: z.number(),
  statusCounts: z.record(z.string(), z.number()),
  categories: z.array(roomCategoryGroupSchema),
});

export type FloorViewSchemaType = z.infer<typeof floorViewSchema>;
export const floorViewListSchema = z.array(floorViewSchema);

// ── Room Types ───────────────────────────────────────────────────────────────

export const roomTypeImageSchema = z.object({
  id: z.string(),
  roomTypeId: z.string(),
  imageUrl: z.string(),
  displayOrder: z.number(),
  isPrimary: z.boolean(),
});

export const roomTypeSchema = z.object({
  id: z.string(),
  hotelId: z.string(),
  name: z.string(),
  title: z.string(),
  bedType: z.string(),
  capacity: z.number(),
  roomSizeSqFt: z.number(),
  pricePerNight: z.number(),
  cleaningFee: z.number(),
  amenitiesFee: z.number(),
  longDescription: z.string(),
  highlights: z.array(z.string()),
  amenities: z.array(z.string()),
  cancellationPolicyText: z.string(),
  isPublished: z.boolean(),
  isActive: z.boolean(),
  images: z.array(roomTypeImageSchema),
});

export type RoomTypeSchemaType = z.infer<typeof roomTypeSchema>;
export const roomTypesListSchema = z.array(roomTypeSchema);

// ── Hotel ────────────────────────────────────────────────────────────────────

export const hotelSchema = z.object({
  id: z.string(),
  name: z.string(),
  address: z.string(),
  phone: z.string(),
  email: z.string(),
  totalFloors: z.number(),
  totalRooms: z.number(),
});

export type HotelSchemaType = z.infer<typeof hotelSchema>;
export const hotelsListSchema = z.array(hotelSchema);

// ── Department ───────────────────────────────────────────────────────────────

export const departmentSchema = z.object({
  id: z.string(),
  name: z.string(),
  description: z.string(),
  managerId: z.string().nullable().optional(),
  managerName: z.string().nullable().optional(),
  employeeCount: z.number(),
  employees: z.array(z.object({
    id: z.string(),
    fullName: z.string(),
    email: z.string(),
    role: z.string(),
  })),
});

export type DepartmentSchemaType = z.infer<typeof departmentSchema>;
export const departmentsListSchema = z.array(departmentSchema);

// ── Complaints ───────────────────────────────────────────────────────────────

export const complaintTimelineEntrySchema = z.object({
  id: z.string(),
  fromStatus: z.union([z.number(), z.string()]),
  toStatus: z.union([z.number(), z.string()]),
  note: z.string(),
  changedBy: z.string(),
  timestampUtc: z.string(),
});

export const complaintSchema = z.object({
  id: z.string(),
  bookingId: z.string().nullable().optional(),
  customerId: z.string(),
  category: z.union([z.number(), z.string()]),
  severity: z.union([z.number(), z.string()]),
  status: z.union([z.number(), z.string()]),
  title: z.string(),
  description: z.string(),
  slaDeadlineUtc: z.string(),
  isOverdue: z.boolean().optional(),
  resolutionNotes: z.string().nullable().optional(),
  resolvedAtUtc: z.string().nullable().optional(),
  createdAtUtc: z.string(),
  timeline: z.array(complaintTimelineEntrySchema).optional().default([]),
});

export type ComplaintSchemaType = z.infer<typeof complaintSchema>;
export const complaintsListSchema = z.array(complaintSchema);

// ── Reviews ──────────────────────────────────────────────────────────────────

export const reviewSchema = z.object({
  id: z.string(),
  bookingId: z.string(),
  customerId: z.string(),
  roomTypeId: z.string(),
  rating: z.number(),
  comment: z.string(),
  isPublished: z.boolean(),
  createdAtUtc: z.string(),
});

export type ReviewSchemaType = z.infer<typeof reviewSchema>;
export const reviewsListSchema = z.array(reviewSchema);
