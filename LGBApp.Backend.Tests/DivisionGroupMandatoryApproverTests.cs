using LGBApp.Backend.Controllers;
using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Models.DTOs;
using LGBApp.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Tests;

/// <summary>Item 8 — the MS5 mandatory approver list must be settable through the API, not only the seeder.</summary>
public class DivisionGroupMandatoryApproverTests
{
    [Fact]
    public async Task Update_PersistsMandatoryApprovers_AndTrimsBlanks()
    {
        using var db = new TestDbFactory();
        WorkflowConfigSeeder.SeedReferenceData(db.Context);

        var group = await db.Context.DivisionGroups
            .Include(g => g.Recommenders)
            .FirstAsync(g => g.Code == "LGB");
        Assert.Empty(TemplateMapper.ReadMandatoryMoaApprovers(group));

        var controller = new DivisionGroupsController(db.Context);
        var dto = TemplateMapper.ToDto(group);
        dto.MandatoryMoaApprovers = ["  Datin Irene  ", "", "Tai Kok Hong", "datin irene"];

        var result = await controller.Update(group.DivisionGroupId, dto);
        Assert.IsType<NoContentResult>(result);

        var saved = await db.Context.DivisionGroups.FirstAsync(g => g.Code == "LGB");
        Assert.Equal(
            new List<string> { "Datin Irene", "Tai Kok Hong" },
            TemplateMapper.ReadMandatoryMoaApprovers(saved));
    }

    [Fact]
    public async Task SavedList_DrivesMs5AssigneeName()
    {
        using var db = new TestDbFactory();
        WorkflowConfigSeeder.SeedReferenceData(db.Context);

        var group = await db.Context.DivisionGroups
            .Include(g => g.Recommenders)
            .FirstAsync(g => g.Code == "LGB");
        var controller = new DivisionGroupsController(db.Context);
        var dto = TemplateMapper.ToDto(group);
        dto.MandatoryMoaApprovers = ["Datin Irene", "Tai Kok Hong"];
        await controller.Update(group.DivisionGroupId, dto);

        var customer = db.SeedCustomer();
        customer.DivisionGroupCode = "LGB";
        db.Context.SaveChanges();

        var job = db.SeedServiceJob(customer);
        var moa = db.SeedMoa(job, db.SeedMoi(job, workflowState: MoiWorkflowStates.Approved));
        var instance = await WorkflowService.InitializeMoaWorkflowAsync(db.Context, moa, customer);

        var ms5 = instance.Steps.Single(s => s.StepKey == "GroupMandatory");
        Assert.Equal("Datin Irene, Tai Kok Hong", ms5.AssigneeName);
    }

    [Fact]
    public async Task Dto_RoundTripsExistingSeededGroups()
    {
        using var db = new TestDbFactory();
        WorkflowConfigSeeder.SeedReferenceData(db.Context);

        var controller = new DivisionGroupsController(db.Context);
        var groups = (await controller.GetAll()).Value!.ToList();

        var bellworth = groups.Single(g => g.Code == "BELLWORTH");
        Assert.Equal(["Kevin Kuok"], bellworth.MandatoryMoaApprovers);

        var swm = groups.Single(g => g.Code == "SWM");
        Assert.Equal(["Janice Lim", "Ho De Leong", "Shirley Nicholas"], swm.MandatoryMoaApprovers);
    }
}
