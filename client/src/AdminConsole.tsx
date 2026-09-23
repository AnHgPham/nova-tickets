import { FormEvent, useEffect, useMemo, useState } from "react";
import { AlertCircle, ChevronLeft, Code2, Database, Edit3, Plus, RefreshCw, Trash2, UsersRound } from "lucide-react";
import { api, money } from "./api";

type Resource = { key: string; label: string; path: string; collectionPath?: string; help: string; template: Record<string, unknown>; idField?: string; pathFor?: (item: Item) => string };
type Item = Record<string, unknown>;

const RESOURCES: Resource[] = [
  { key: "events", label: "Sự kiện", path: "/events", collectionPath: "/events?includeDrafts=true", help: "Tạo và cập nhật event; việc xóa bị chặn khi đã có suất diễn/booking liên quan.", template: { title: "Tên sự kiện", slug: "ten-su-kien", artist: "Nghệ sĩ", description: "Mô tả", heroImageUrl: "/manus-storage/hero-concert_81d2138b.jpg", minPrice: 690000, status: "Draft" } },
  { key: "venues", label: "Địa điểm", path: "/venues", help: "CRUD địa điểm, thành phố và địa chỉ.", template: { name: "Tên địa điểm", city: "TP. Hồ Chí Minh", address: "Địa chỉ", description: "Mô tả", isActive: true } },
  { key: "performances", label: "Suất diễn", path: "/performances", help: "Gắn event và venue bằng UUID từ hai tab tương ứng; điền giá Standard và VIP khi khởi tạo seat inventory.", template: { eventId: "", venueId: "", startsAtUtc: "2026-10-01T13:00:00Z", doorsOpenAtUtc: "2026-10-01T11:00:00Z", salesStartUtc: "2026-08-26T00:00:00Z", salesEndUtc: "2026-10-01T15:00:00Z", status: "Draft", standardPrice: 690000, vipPrice: 1490000 } },
  { key: "seats", label: "Ghế", path: "/seats", help: "Quản lý sơ đồ ghế. SeatAreaId và TicketTypeId là tùy chọn.", template: { venueId: "", section: "A", rowLabel: "A", number: 1, positionX: 1, positionY: 1, priceTier: "STANDARD", isActive: true } },
  { key: "areas", label: "Khu vực ghế", path: "/seat-areas", help: "Nhóm ghế theo khu vực, màu hiển thị và thứ tự trên sơ đồ.", template: { venueId: "", code: "VIP", name: "VIP Floor", color: "#D8FF57", sortOrder: 1, isActive: true } },
  { key: "ticketTypes", label: "Loại vé", path: "/ticket-types", help: "Định nghĩa tier vé. Giá cho từng suất diễn cấu hình tại API Console bên dưới.", template: { code: "VIP", name: "VIP Experience", description: "Mô tả quyền lợi", color: "#D8FF57", basePrice: 1490000, isActive: true } },
  { key: "performancePrices", label: "Giá suất diễn", path: "/admin/performance-ticket-types", help: "Cấu hình loại vé, giá và sức chứa cho từng suất diễn bằng UUID performance và ticket type.", template: { performanceId: "", ticketTypeId: "", price: 690000, capacity: 40, isActive: true }, pathFor: (item) => `/admin/performance-ticket-types/${item.performanceId}/${item.ticketTypeId}` },
  { key: "users", label: "Người dùng", path: "/admin/users", help: "Tạo, phân role, vô hiệu hóa tài khoản. Delete là deactivate để bảo toàn lịch sử.", template: { email: "staff@novatickets.vn", password: "StrongPass@2026", fullName: "Nhân viên vận hành", phone: "0900000000", role: "Staff" } },
  { key: "bookings", label: "Booking", path: "/admin/bookings", help: "Theo dõi, cập nhật liên hệ/trạng thái và hủy đơn chưa thanh toán một cách an toàn.", template: { customerName: "Khách hàng", customerEmail: "customer@example.com", customerPhone: "0900000000" } },
];

function formatCell(value: unknown) {
  if (value === null || value === undefined) return "—";
  if (typeof value === "object") return Array.isArray(value) ? `${value.length} mục` : "{" + Object.keys(value as object).length + "}";
  return String(value).length > 36 ? String(value).slice(0, 35) + "…" : String(value);
}

export default function AdminConsole({ onBack, onRefresh }: { onBack: () => void; onRefresh: () => void }) {
  const [resourceKey, setResourceKey] = useState("events");
  const [items, setItems] = useState<Item[]>([]);
  const [selected, setSelected] = useState<Item | null>(null);
  const [draft, setDraft] = useState("");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ kind: "ok" | "error"; text: string } | null>(null);
  const resource = useMemo(() => RESOURCES.find((entry) => entry.key === resourceKey)!, [resourceKey]);
  const idField = resource.idField ?? "id";

  const reload = async () => {
    setBusy(true); setMessage(null);
    try {
      const result = await api<unknown[]>(resource.collectionPath ?? resource.path);
      setItems(Array.isArray(result) ? result as Item[] : []);
    } catch (error) { setItems([]); setMessage({ kind: "error", text: error instanceof Error ? error.message : "Không thể tải dữ liệu." }); }
    finally { setBusy(false); }
  };
  useEffect(() => { setSelected(null); setDraft(JSON.stringify(resource.template, null, 2)); void reload(); }, [resourceKey]);

  const select = (item: Item) => { setSelected(item); setDraft(JSON.stringify(item, null, 2)); };
  const submit = async (mode: "create" | "update") => {
    let body: Record<string, unknown>;
    try { body = JSON.parse(draft) as Record<string, unknown>; } catch { setMessage({ kind: "error", text: "JSON chưa hợp lệ." }); return; }
    const id = selected?.[idField];
    if (mode === "update" && !id) { setMessage({ kind: "error", text: "Hãy chọn một bản ghi để cập nhật." }); return; }
    setBusy(true); setMessage(null);
    try {
      const url = mode === "create" ? resource.path : (resource.pathFor ? resource.pathFor(selected!) : `${resource.path}/${id}`);
      const result = await api<Item>(url, { method: mode === "create" ? "POST" : "PUT", body: JSON.stringify(body) });
      setMessage({ kind: "ok", text: mode === "create" ? "Đã tạo bản ghi." : "Đã cập nhật bản ghi." });
      setSelected(result); setDraft(JSON.stringify(result, null, 2)); await reload(); await onRefresh();
    } catch (error) { setMessage({ kind: "error", text: error instanceof Error ? error.message : "Không thể lưu." }); }
    finally { setBusy(false); }
  };
  const remove = async () => {
    const id = selected?.[idField]; if (!id) { setMessage({ kind: "error", text: "Hãy chọn một bản ghi để xóa hoặc vô hiệu hóa." }); return; }
    if (!window.confirm("Xác nhận thao tác này? Dữ liệu có ràng buộc sẽ được API bảo vệ.")) return;
    setBusy(true); setMessage(null);
    try { await api(resource.pathFor ? resource.pathFor(selected!) : `${resource.path}/${id}`, { method: "DELETE" }); setMessage({ kind: "ok", text: "Đã xử lý thành công." }); setSelected(null); setDraft(JSON.stringify(resource.template, null, 2)); await reload(); await onRefresh(); }
    catch (error) { setMessage({ kind: "error", text: error instanceof Error ? error.message : "Không thể xử lý." }); }
    finally { setBusy(false); }
  };

  const columns = items.length ? Object.keys(items[0]).filter((key) => !["description", "heroImageUrl", "passwordHash", "tickets"].includes(key)).slice(0, 5) : [];
  return <main className="admin-console"><button className="back-link" onClick={onBack}><ChevronLeft size={16}/> Quay lại storefront</button><div className="console-heading"><div><p className="eyebrow">ADMIN OPERATIONS</p><h1>Quản trị vận hành</h1><p>Console nội bộ gọi trực tiếp REST API .NET đã phân quyền. Các lệnh bị backend kiểm tra validation, audit và ràng buộc dữ liệu.</p></div><button className="outline-button" onClick={() => window.location.assign("/swagger")}><Code2 size={16}/> OpenAPI / Swagger</button></div><div className="console-layout"><aside className="resource-nav"><div className="resource-nav-title"><Database size={17}/> RESOURCE</div>{RESOURCES.map((entry) => <button className={entry.key === resourceKey ? "active" : ""} onClick={() => setResourceKey(entry.key)} key={entry.key}>{entry.key === "users" ? <UsersRound size={15}/> : <Database size={15}/>} {entry.label}</button>)}</aside><section className="console-workspace"><div className="resource-header"><div><h2>{resource.label}</h2><p>{resource.help}</p></div><button className="icon-button" disabled={busy} onClick={reload}><RefreshCw size={17}/></button></div>{message && <div className={`console-message ${message.kind}`}><AlertCircle size={16}/>{message.text}</div>}<div className="console-grid"><div className="data-table"><div className="table-label"><span>{items.length} bản ghi</span><span>Chọn dòng để chỉnh sửa</span></div><div className="data-scroll"><table><thead><tr>{columns.map((column) => <th key={column}>{column}</th>)}</tr></thead><tbody>{items.map((item, index) => <tr className={selected?.[idField] === item[idField] ? "selected" : ""} onClick={() => select(item)} key={String(item[idField] ?? index)}>{columns.map((column) => <td key={column}>{column.toLowerCase().includes("price") && typeof item[column] === "number" ? money(item[column] as number) : formatCell(item[column])}</td>)}</tr>)}</tbody></table>{!items.length && !busy && <p className="no-data">Chưa có dữ liệu hoặc bạn không có quyền xem resource này.</p>}</div></div><form className="json-editor" onSubmit={(event: FormEvent) => { event.preventDefault(); void submit(selected ? "update" : "create"); }}><div className="editor-label"><span>{selected ? "EDIT SELECTED" : "CREATE NEW"}</span><button type="button" className="text-button" onClick={() => { setSelected(null); setDraft(JSON.stringify(resource.template, null, 2)); }}>Mẫu mới</button></div><textarea aria-label="Dữ liệu JSON" spellCheck={false} value={draft} onChange={(event) => setDraft(event.target.value)}/><div className="editor-actions"><button className="primary-button" disabled={busy} type="submit">{selected ? <><Edit3 size={16}/> Cập nhật</> : <><Plus size={16}/> Tạo mới</>}</button><button className="danger-button" disabled={busy || !selected} type="button" onClick={remove}><Trash2 size={16}/> Xóa / vô hiệu hóa</button></div></form></div></section></div></main>;
}
