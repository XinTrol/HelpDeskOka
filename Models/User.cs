using System;
using System.Collections.Generic;

namespace DiplomHelpDeskOka.Models;

public partial class User
{
    public long Id { get; set; }

    public string? Phone { get; set; }

    public string Email { get; set; } = null!;

    public string Login { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public long? PositionId { get; set; }

    public long? DepartmentId { get; set; }

    public long RoleId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public string? Patronymic { get; set; }

    public string? Name { get; set; }

    public string? Surname { get; set; }

    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public virtual Department? Department { get; set; }

    public virtual Position? Position { get; set; }

    public virtual Role Role { get; set; } = null!;

    public virtual ICollection<Ticket> TicketAuthors { get; set; } = new List<Ticket>();

    public virtual ICollection<TicketHistory> TicketHistories { get; set; } = new List<TicketHistory>();

    public virtual ICollection<Ticket> TicketResponsibleUsers { get; set; } = new List<Ticket>();

    public string FullName => $"{Surname} {Name} {Patronymic}";

    public string ShortName => $"{Surname} {GetInitial(Name)}. {GetInitial(Patronymic)}.";
    private string GetInitial(string name)
    {
        return string.IsNullOrEmpty(name) ? "" : name[0].ToString().ToUpper();
    }
}
