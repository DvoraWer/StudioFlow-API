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

    [Theory]
    [InlineData(nameof(InstructorsController.GetAll))]
    [InlineData(nameof(InstructorsController.GetById))]
    [InlineData(nameof(InstructorsController.Create))]
    [InlineData(nameof(InstructorsController.Update))]
    [InlineData(nameof(InstructorsController.Delete))]
    public void Instructor_management_remains_Admin_only(string action)
    {
        Assert.True(Allows(typeof(InstructorsController), action, "Admin"));
        Assert.False(Allows(typeof(InstructorsController), action, "Instructor"));
        Assert.False(Allows(typeof(InstructorsController), action, "Member"));
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Instructor")]
    [InlineData("Member")]
    public void My_account_is_open_to_every_authenticated_role(string role)
        => Assert.True(Allows(typeof(MeController), nameof(MeController.GetAccount), role));

    [Theory]
    [InlineData("Admin")]
    [InlineData("Instructor")]
    [InlineData("Member")]
    public void Change_password_requires_authentication_and_is_open_to_every_role(string role)
        => Assert.True(Allows(typeof(AuthController), nameof(AuthController.ChangePassword), role)); // RoleSets also asserts no [AllowAnonymous]

    [Theory]
    [InlineData(nameof(MeController.GetInstructorProfile))]
    [InlineData(nameof(MeController.UpdateInstructorProfile))]
    public void Instructor_profile_is_Instructor_only(string action)
    {
        Assert.True(Allows(typeof(MeController), action, "Instructor"));
        Assert.False(Allows(typeof(MeController), action, "Admin"));
        Assert.False(Allows(typeof(MeController), action, "Member"));
    }

    [Theory]
    [InlineData(nameof(MeController.GetAccount))]
    [InlineData(nameof(MeController.GetInstructorProfile))]
    [InlineData(nameof(MeController.UpdateInstructorProfile))]
    public void Me_actions_take_no_client_supplied_id(string action)
    {
        var method = typeof(MeController).GetMethod(action)!;
        Assert.DoesNotContain(method.GetParameters(), p => p.ParameterType == typeof(int));
        Assert.DoesNotContain("{", method.GetCustomAttributes<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().Single().Template);
    }

    [Theory]
    [InlineData(nameof(RegistrationsController.Register))]
    [InlineData(nameof(RegistrationsController.Cancel))]
    [InlineData(nameof(RegistrationsController.GetMyRegistrations))]
    public void Member_registration_endpoints_remain_Member_only(string action)
    {
        Assert.True(Allows(typeof(RegistrationsController), action, "Member"));
        Assert.False(Allows(typeof(RegistrationsController), action, "Admin"));
        Assert.False(Allows(typeof(RegistrationsController), action, "Instructor"));
    }
}
