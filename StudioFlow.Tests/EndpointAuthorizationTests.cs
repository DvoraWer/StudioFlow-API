using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using StudioFlow.API.Controllers;

namespace StudioFlow.Tests;

/// <summary>
/// Locks down which roles can reach the class and room endpoints. ASP.NET Core
/// combines every [Authorize] on the controller and the action with AND, and an
/// endpoint without [AllowAnonymous] rejects unauthenticated callers with 401, so
/// the declared roles are exactly the roles that get through.
/// </summary>
public class EndpointAuthorizationTests
{
    private static IReadOnlyList<string[]> RoleSets(Type controller, string action)
    {
        var method = controller.GetMethod(action, BindingFlags.Public | BindingFlags.Instance)
                     ?? throw new InvalidOperationException($"{controller.Name}.{action} not found.");

        Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Null(controller.GetCustomAttribute<AllowAnonymousAttribute>());

        var attributes = controller.GetCustomAttributes<AuthorizeAttribute>()
            .Concat(method.GetCustomAttributes<AuthorizeAttribute>())
            .ToList();

        Assert.NotEmpty(attributes); // authenticated callers only
        return attributes
            .Select(a => (a.Roles ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .ToList();
    }

    /// <summary>A role passes only if every [Authorize] in the chain admits it.</summary>
    private static bool Allows(Type controller, string action, string role) =>
        RoleSets(controller, action).All(roles => roles.Length == 0 || roles.Contains(role));

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Instructor", true)]
    [InlineData("Member", false)]
    public void Create_class_is_open_to_Admin_and_Instructor_only(string role, bool allowed)
        => Assert.Equal(allowed, Allows(typeof(ClassesController), nameof(ClassesController.Create), role));

    [Theory]
    [InlineData(nameof(ClassesController.Update))]
    [InlineData(nameof(ClassesController.Cancel))]
    public void Update_and_cancel_class_remain_Admin_only(string action)
    {
        Assert.True(Allows(typeof(ClassesController), action, "Admin"));
        Assert.False(Allows(typeof(ClassesController), action, "Instructor"));
        Assert.False(Allows(typeof(ClassesController), action, "Member"));
    }

    [Fact]
    public void Room_list_is_readable_by_Admin_and_Instructor_but_not_Member()
    {
        Assert.True(Allows(typeof(RoomsController), nameof(RoomsController.GetAll), "Admin"));
        Assert.True(Allows(typeof(RoomsController), nameof(RoomsController.GetAll), "Instructor"));
        Assert.False(Allows(typeof(RoomsController), nameof(RoomsController.GetAll), "Member"));
    }

    [Theory]
    [InlineData(nameof(RoomsController.GetById))]
    [InlineData(nameof(RoomsController.Create))]
    [InlineData(nameof(RoomsController.Update))]
    [InlineData(nameof(RoomsController.Delete))]
    public void Room_management_remains_Admin_only(string action)
    {
        Assert.True(Allows(typeof(RoomsController), action, "Admin"));
        Assert.False(Allows(typeof(RoomsController), action, "Instructor"));
        Assert.False(Allows(typeof(RoomsController), action, "Member"));
    }
}
