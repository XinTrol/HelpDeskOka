using System;
using System.Collections.Generic;

namespace DiplomHelpDeskOka.Models;

public partial class TicketHistory
{
    public long Id { get; set; }

    public long TicketId { get; set; }

    public long ChangedByUserId { get; set; }

    public DateTime ChangeDate { get; set; }

    public string FieldName { get; set; } = null!;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public virtual User ChangedByUser { get; set; } = null!;

    public virtual Ticket Ticket { get; set; } = null!;
}
