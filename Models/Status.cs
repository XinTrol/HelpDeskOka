using System;
using System.Collections.Generic;

namespace DiplomHelpDeskOka.Models;

public partial class Status
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
