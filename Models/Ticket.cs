using System;
using System.Collections.Generic;

namespace DiplomHelpDeskOka.Models;

public partial class Ticket
{
    public long Id { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public DateTime? PlannedCompletionDate { get; set; }

    public long TicketTypeId { get; set; }

    public long StatusId { get; set; }

    public long PriorityId { get; set; }

    public long AuthorId { get; set; }

    public long DepartmentId { get; set; }

    public long? ResponsibleUserId { get; set; }

    public virtual User Author { get; set; } = null!;

    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public virtual Department Department { get; set; } = null!;

    public virtual Priority Priority { get; set; } = null!;

    public virtual User? ResponsibleUser { get; set; }

    public virtual Status Status { get; set; } = null!;

    public virtual ICollection<TicketHistory> TicketHistories { get; set; } = new List<TicketHistory>();

    public virtual TicketType TicketType { get; set; } = null!;

    public string TicketTypeName => TicketType?.Name ?? string.Empty;

    public string StatusName => Status?.Name ?? string.Empty;

    public string DepartmentName => Department?.Name ?? string.Empty;

    public string PriorityName => Priority?.Name ?? string.Empty;

    public string AuthorName => Author?.ShortName ?? string.Empty;
}
