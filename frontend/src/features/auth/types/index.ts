export interface CustomerLoginResponse {
  token: string;
  customerId: string;
  fullName: string;
  email: string;
  expiresAt: string;
}

export interface EmployeeLoginResponse {
  token: string;
  employeeId: string;
  fullName: string;
  email: string;
  role: number;
  expiresAt: string;
}

export interface RegisterCustomerResponse {
  customerId: string;
}
