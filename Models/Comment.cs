using System;
using System.Collections.Generic;

namespace DiplomHelpDeskOka.Models;

public partial class Comment
{
    public long Id { get; set; }

    public long TicketId { get; set; }

    public long UserId { get; set; }

    public string Text { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Ticket Ticket { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
