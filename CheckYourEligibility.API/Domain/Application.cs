﻿// Ignore Spelling: Fsm

using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Domain;

[ExcludeFromCodeCoverage(Justification = "Data Model.")]
public class Application
{
    public string ApplicationID { get; set; }

    [Column(TypeName = "varchar(100)")] public CheckEligibilityType Type { get; set; }

    [Column(TypeName = "varchar(8)")] public string Reference { get; set; }

    public int LocalAuthorityId { get; set; }

    public virtual Establishment Establishment { get; set; }
    public int EstablishmentId { get; set; }

    [Column(TypeName = "varchar(100)")] public string ParentFirstName { get; set; }

    [Column(TypeName = "varchar(100)")] public string ParentLastName { get; set; }

    [Column(TypeName = "varchar(50)")] public string? ParentNationalInsuranceNumber { get; set; }

    [Column(TypeName = "varchar(50)")] public string? ParentNationalAsylumSeekerServiceNumber { get; set; }

    public DateTime ParentDateOfBirth { get; set; }

    [Column(TypeName = "varchar(50)")] public string ChildFirstName { get; set; }

    [Column(TypeName = "varchar(50)")] public string ChildLastName { get; set; }

    public DateTime ChildDateOfBirth { get; set; }

    public DateTime Created { get; set; }

    public DateTime Updated { get; set; }
    public DateTime? EligibilityEndDate { get; set; }

    public virtual IEnumerable<ApplicationStatus> Statuses { get; set; }

    [Column(TypeName = "varchar(100)")] public Enums.ApplicationStatus? Status { get; set; }

    public virtual User User { get; set; }
    public string? UserId { get; set; }
    public virtual EligibilityCheckHash EligibilityCheckHash { get; set; }
    public string? EligibilityCheckHashID { get; set; }

    [Column(TypeName = "varchar(1000)")] public string ParentEmail { get; set; }
    public virtual ICollection<ApplicationEvidence> Evidence { get; set; } = new List<ApplicationEvidence>();
}