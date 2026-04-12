using System.Collections;
using System.IO;
using System.Management.Automation;
using GitHub.Copilot.SDK;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.Unit;

[Trait("Category", "Unit")]
public class SessionCmdletTests
{
    [Fact]
    public void NewCopilotSession_HasCorrectCmdletAttribute()
    {
        var attr = (CmdletAttribute)Attribute.GetCustomAttribute(
            typeof(NewCopilotSessionCmdlet), typeof(CmdletAttribute))!;
        Assert.Equal(VerbsCommon.New, attr.VerbName);
        Assert.Equal("CopilotSession", attr.NounName);
    }

    [Fact]
    public void NewCopilotSession_HasOutputType()
    {
        var attrs = Attribute.GetCustomAttributes(
            typeof(NewCopilotSessionCmdlet), typeof(OutputTypeAttribute));
        Assert.Single(attrs);
        var outputType = (OutputTypeAttribute)attrs[0];
        Assert.Contains(outputType.Type, t => t.Type == typeof(CopilotSession));
    }

    [Fact]
    public void ResumeCopilotSession_HasMandatorySessionIdParameter()
    {
        var prop = typeof(ResumeCopilotSessionCmdlet).GetProperty("SessionId")!;
        var paramAttr = (ParameterAttribute)Attribute.GetCustomAttribute(prop, typeof(ParameterAttribute))!;
        Assert.True(paramAttr.Mandatory);
        Assert.Equal(0, paramAttr.Position);
    }

    [Fact]
    public void ResumeCopilotSession_HasCorrectCmdletAttribute()
    {
        var attr = (CmdletAttribute)Attribute.GetCustomAttribute(
            typeof(ResumeCopilotSessionCmdlet), typeof(CmdletAttribute))!;
        Assert.Equal("Resume", attr.VerbName);
        Assert.Equal("CopilotSession", attr.NounName);
    }

    [Fact]
    public void GetCopilotSession_HasCorrectCmdletAttribute()
    {
        var attr = (CmdletAttribute)Attribute.GetCustomAttribute(
            typeof(GetCopilotSessionCmdlet), typeof(CmdletAttribute))!;
        Assert.Equal(VerbsCommon.Get, attr.VerbName);
        Assert.Equal("CopilotSession", attr.NounName);
    }

    [Fact]
    public void GetCopilotSession_OutputsSessionMetadata()
    {
        var attrs = Attribute.GetCustomAttributes(
            typeof(GetCopilotSessionCmdlet), typeof(OutputTypeAttribute));
        Assert.Single(attrs);
        var outputType = (OutputTypeAttribute)attrs[0];
        Assert.Contains(outputType.Type, t => t.Type == typeof(SessionMetadata));
    }

    [Fact]
    public void RemoveCopilotSession_SupportsShouldProcess()
    {
        var attr = (CmdletAttribute)Attribute.GetCustomAttribute(
            typeof(RemoveCopilotSessionCmdlet), typeof(CmdletAttribute))!;
        Assert.True(attr.SupportsShouldProcess);
    }

    [Fact]
    public void RemoveCopilotSession_HasMandatorySessionIdParameter()
    {
        var prop = typeof(RemoveCopilotSessionCmdlet).GetProperty("SessionId")!;
        var paramAttr = (ParameterAttribute)Attribute.GetCustomAttribute(prop, typeof(ParameterAttribute))!;
        Assert.True(paramAttr.Mandatory);
        Assert.Equal(0, paramAttr.Position);
    }

    [Fact]
    public void CloseCopilotSession_HasCorrectCmdletAttribute()
    {
        var attr = (CmdletAttribute)Attribute.GetCustomAttribute(
            typeof(CloseCopilotSessionCmdlet), typeof(CmdletAttribute))!;
        Assert.Equal(VerbsCommon.Close, attr.VerbName);
        Assert.Equal("CopilotSession", attr.NounName);
    }

    [Fact]
    public async Task PermissionHandlers_AutoApproveReturnsApproval()
    {
        var handler = PermissionHandlers.AutoApprove;
        var request = new PermissionRequest { Kind = "tool_use" };
        var invocation = new PermissionInvocation();

        var result = await handler.Invoke(request, invocation);

        Assert.Equal("approved", result.Kind.ToString());
    }

    [Fact]
    public async Task PermissionHandlers_InteractiveApprovesOnY()
    {
        var handler = PermissionHandlers.Interactive;
        var request = new PermissionRequest { Kind = "tool_use" };
        var invocation = new PermissionInvocation();

        var originalIn = Console.In;
        var originalErr = Console.Error;
        try
        {
            Console.SetIn(new StringReader("y"));
            Console.SetError(TextWriter.Null);
            var result = await handler.Invoke(request, invocation);
            Assert.Equal("approved", result.Kind.ToString());
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public async Task PermissionHandlers_InteractiveDeniesOnN()
    {
        var handler = PermissionHandlers.Interactive;
        var request = new PermissionRequest { Kind = "tool_use" };
        var invocation = new PermissionInvocation();

        var originalIn = Console.In;
        var originalErr = Console.Error;
        try
        {
            Console.SetIn(new StringReader("n"));
            Console.SetError(TextWriter.Null);
            var result = await handler.Invoke(request, invocation);
            Assert.Equal("denied-interactively-by-user", result.Kind.ToString());
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void UserInputHandlers_InteractiveIsNotNull()
    {
        var handler = UserInputHandlers.Interactive;
        Assert.NotNull(handler);
    }

    [Fact]
    public void NewCopilotSession_HasEnableConfigDiscoveryParameter()
    {
        var prop = typeof(NewCopilotSessionCmdlet).GetProperty("EnableConfigDiscovery")!;
        Assert.NotNull(prop);
        Assert.Equal(typeof(SwitchParameter), prop.PropertyType);
    }

    [Fact]
    public void NewCopilotSession_HasAgentParameter()
    {
        var prop = typeof(NewCopilotSessionCmdlet).GetProperty("Agent")!;
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop.PropertyType);
    }

    [Fact]
    public void ResumeCopilotSession_HasEnableConfigDiscoveryParameter()
    {
        var prop = typeof(ResumeCopilotSessionCmdlet).GetProperty("EnableConfigDiscovery")!;
        Assert.NotNull(prop);
        Assert.Equal(typeof(SwitchParameter), prop.PropertyType);
    }

    [Fact]
    public void ResumeCopilotSession_HasAgentParameter()
    {
        var prop = typeof(ResumeCopilotSessionCmdlet).GetProperty("Agent")!;
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop.PropertyType);
    }

    [Fact]
    public void GetCopilotSession_HasOptionalSessionIdParameter()
    {
        var prop = typeof(GetCopilotSessionCmdlet).GetProperty("SessionId")!;
        Assert.NotNull(prop);
        var paramAttr = (ParameterAttribute)Attribute.GetCustomAttribute(prop, typeof(ParameterAttribute))!;
        Assert.False(paramAttr.Mandatory);
        Assert.Equal(0, paramAttr.Position);
    }

    [Fact]
    public void GetCopilotSession_SessionIdHasArgumentCompleter()
    {
        var prop = typeof(GetCopilotSessionCmdlet).GetProperty("SessionId")!;
        var attr = Attribute.GetCustomAttribute(prop, typeof(ArgumentCompleterAttribute));
        Assert.NotNull(attr);
        Assert.Equal(typeof(CopilotSessionCompleter), ((ArgumentCompleterAttribute)attr).Type);
    }

    [Fact]
    public void NewCopilotSession_HasSkillDirectoriesParameter()
    {
        var prop = typeof(NewCopilotSessionCmdlet).GetProperty("SkillDirectories")!;
        Assert.NotNull(prop);
        Assert.Equal(typeof(string[]), prop.PropertyType);
    }

    [Fact]
    public void ResumeCopilotSession_HasSkillDirectoriesParameter()
    {
        var prop = typeof(ResumeCopilotSessionCmdlet).GetProperty("SkillDirectories")!;
        Assert.NotNull(prop);
        Assert.Equal(typeof(string[]), prop.PropertyType);
    }

    [Fact]
    public void NewCopilotSession_HasSystemMessageModeParameter()
    {
        var prop = typeof(NewCopilotSessionCmdlet).GetProperty("SystemMessageMode")!;
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop.PropertyType);
    }

    [Fact]
    public void ResumeCopilotSession_HasSystemMessageModeParameter()
    {
        var prop = typeof(ResumeCopilotSessionCmdlet).GetProperty("SystemMessageMode")!;
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop.PropertyType);
    }

    [Fact]
    public void NewCopilotSession_HasSystemMessageSectionsParameter()
    {
        var prop = typeof(NewCopilotSessionCmdlet).GetProperty("SystemMessageSections")!;
        Assert.NotNull(prop);
        Assert.Equal(typeof(Hashtable), prop.PropertyType);
    }

    [Fact]
    public void ResumeCopilotSession_HasSystemMessageSectionsParameter()
    {
        var prop = typeof(ResumeCopilotSessionCmdlet).GetProperty("SystemMessageSections")!;
        Assert.NotNull(prop);
        Assert.Equal(typeof(Hashtable), prop.PropertyType);
    }

    [Fact]
    public void SystemMessageHelper_ReturnsNullWhenAllInputsNull()
    {
        var result = SystemMessageHelper.Build(null, null, null);
        Assert.Null(result);
    }

    [Fact]
    public void SystemMessageHelper_SetsContentOnly()
    {
        var result = SystemMessageHelper.Build("hello", null, null)!;
        Assert.Equal("hello", result.Content);
        Assert.Null(result.Mode);
        Assert.Null(result.Sections);
    }

    [Fact]
    public void SystemMessageHelper_SetsModeOnly()
    {
        var result = SystemMessageHelper.Build(null, "Append", null)!;
        Assert.Null(result.Content);
        Assert.Equal(GitHub.Copilot.SDK.SystemMessageMode.Append, result.Mode);
    }

    [Fact]
    public void SystemMessageHelper_ParsesModeIgnoringCase()
    {
        var result = SystemMessageHelper.Build(null, "replace", null)!;
        Assert.Equal(GitHub.Copilot.SDK.SystemMessageMode.Replace, result.Mode);
    }

    [Fact]
    public void SystemMessageHelper_BuildsSectionsFromHashtable()
    {
        var sections = new Hashtable
        {
            ["behavior"] = new Hashtable { ["Action"] = "Replace", ["Content"] = "Be concise" },
            ["tools"] = new Hashtable { ["Action"] = "Remove" }
        };

        var result = SystemMessageHelper.Build(null, "Customize", sections)!;
        Assert.Equal(GitHub.Copilot.SDK.SystemMessageMode.Customize, result.Mode);
        Assert.Equal(2, result.Sections!.Count);
        Assert.Equal(SectionOverrideAction.Replace, result.Sections["behavior"].Action);
        Assert.Equal("Be concise", result.Sections["behavior"].Content);
        Assert.Equal(SectionOverrideAction.Remove, result.Sections["tools"].Action);
    }

    [Fact]
    public void SystemMessageHelper_IgnoresNonHashtableAndNonSectionOverrideValues()
    {
        var sections = new Hashtable
        {
            ["valid"] = new Hashtable { ["Action"] = "Append", ["Content"] = "extra" },
            ["invalid"] = "just a string"
        };

        var result = SystemMessageHelper.Build(null, null, sections)!;
        Assert.Single(result.Sections!);
        Assert.True(result.Sections!.ContainsKey("valid"));
    }

    [Fact]
    public void SystemMessageHelper_AcceptsSectionOverrideValues()
    {
        var sections = new Hashtable
        {
            ["behavior"] = new SectionOverride { Action = SectionOverrideAction.Replace, Content = "Be concise" },
            ["tools"] = new SectionOverride { Action = SectionOverrideAction.Remove }
        };

        var result = SystemMessageHelper.Build(null, "Customize", sections)!;
        Assert.Equal(2, result.Sections!.Count);
        Assert.Equal(SectionOverrideAction.Replace, result.Sections["behavior"].Action);
        Assert.Equal("Be concise", result.Sections["behavior"].Content);
        Assert.Equal(SectionOverrideAction.Remove, result.Sections["tools"].Action);
    }

    [Fact]
    public void SystemMessageHelper_AcceptsMixedSectionValues()
    {
        var sections = new Hashtable
        {
            ["typed"] = new SectionOverride { Action = SectionOverrideAction.Append, Content = "extra" },
            ["hashtable"] = new Hashtable { ["Action"] = "Prepend", ["Content"] = "before" }
        };

        var result = SystemMessageHelper.Build(null, null, sections)!;
        Assert.Equal(2, result.Sections!.Count);
        Assert.Equal(SectionOverrideAction.Append, result.Sections["typed"].Action);
        Assert.Equal(SectionOverrideAction.Prepend, result.Sections["hashtable"].Action);
    }

    [Fact]
    public void NewCopilotSectionOverride_HasCorrectCmdletAttribute()
    {
        var attr = (CmdletAttribute)Attribute.GetCustomAttribute(
            typeof(NewCopilotSectionOverrideCmdlet), typeof(CmdletAttribute))!;
        Assert.Equal(VerbsCommon.New, attr.VerbName);
        Assert.Equal("CopilotSectionOverride", attr.NounName);
    }

    [Fact]
    public void NewCopilotSectionOverride_HasOutputType()
    {
        var attrs = Attribute.GetCustomAttributes(
            typeof(NewCopilotSectionOverrideCmdlet), typeof(OutputTypeAttribute));
        Assert.Single(attrs);
        var outputType = (OutputTypeAttribute)attrs[0];
        Assert.Contains(outputType.Type, t => t.Type == typeof(SectionOverride));
    }

    [Fact]
    public void NewCopilotSectionOverride_ActionIsMandatoryPosition0()
    {
        var prop = typeof(NewCopilotSectionOverrideCmdlet).GetProperty("Action")!;
        var paramAttr = (ParameterAttribute)Attribute.GetCustomAttribute(prop, typeof(ParameterAttribute))!;
        Assert.True(paramAttr.Mandatory);
        Assert.Equal(0, paramAttr.Position);
    }

    [Fact]
    public void NewCopilotSectionOverride_ContentIsOptionalPosition1()
    {
        var prop = typeof(NewCopilotSectionOverrideCmdlet).GetProperty("Content")!;
        var paramAttr = (ParameterAttribute)Attribute.GetCustomAttribute(prop, typeof(ParameterAttribute))!;
        Assert.False(paramAttr.Mandatory);
        Assert.Equal(1, paramAttr.Position);
    }

    [Fact]
    public void NewCopilotSectionOverride_ActionHasArgumentCompleter()
    {
        var prop = typeof(NewCopilotSectionOverrideCmdlet).GetProperty("Action")!;
        var attr = Attribute.GetCustomAttribute(prop, typeof(ArgumentCompleterAttribute));
        Assert.NotNull(attr);
        Assert.Equal(typeof(SectionOverrideActionCompleter), ((ArgumentCompleterAttribute)attr).Type);
    }
}
