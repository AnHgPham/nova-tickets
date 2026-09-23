export type User = { id: string; email: string; fullName: string; phone?: string; role: "Customer" | "Staff" | "Admin"; isActive: boolean };
export type ShowPerformance = { id: string; startsAtUtc: string; doorsOpenAtUtc: string; salesStartUtc: string; salesEndUtc: string; status: string; venueId: string; venue: { name: string; city: string; address: string } };
export type ConcertEvent = { id: string; title: string; slug: string; artist: string; description: string; heroImageUrl: string; minPrice: number; status: string; performances: ShowPerformance[] };
export type Seat = { seatId: string; section: string; rowLabel: string; number: number; price: number; status: "Available" | "Held" | "Sold" | "Blocked"; holdExpiresAtUtc?: string };
export type Hold = { holdToken: string; expiresAtUtc: string; seatIds: string[]; totalAmount: number };
export type Booking = { id: string; bookingCode: string; status: string; totalAmount: number; expiresAtUtc: string; createdAtUtc: string; eventTitle: string; startsAtUtc: string; venueName: string; tickets: { id: string; ticketCode: string; section: string; rowLabel: string; seatNumber: number; price: number }[] };

export class ApiError extends Error { constructor(public status: number, message: string) { super(message); } }

export async function api<T>(path: string, options: RequestInit = {}): Promise<T> {
  const response = await fetch(`/api${path}`, { credentials: "include", headers: { "Content-Type": "application/json", ...(options.headers ?? {}) }, ...options });
  if (response.status === 204) return undefined as T;
  const data = await response.json().catch(() => ({}));
  if (!response.ok) throw new ApiError(response.status, data.title ?? data.message ?? "Yêu cầu không thành công.");
  return data as T;
}

export const money = (value: number) => new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND", maximumFractionDigits: 0 }).format(value);
export const dateTime = (value: string) => new Intl.DateTimeFormat("vi-VN", { weekday: "short", day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" }).format(new Date(value));
