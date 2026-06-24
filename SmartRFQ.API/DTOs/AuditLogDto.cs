

namespace SmartRFQ.API.DTOs;

// GET /api/auditlog — query params
public record AuditLogQueryDto(
    DateTime? DateFrom,
    DateTime? DateTo,
    string?   E_User,
    string?   E_Purchaser,
    string?   RfqNo,
    string?   Status,
    int       Page     = 1,
    int       PageSize = 10
);

// GET response
public record AuditLogResponseDto(
    int     Id,
    string  RfqNo,
    string  Status,
    string  Role,
    string  E_User,
    string  E_Purchaser,
    string? Remark,
    string  DateTime
);