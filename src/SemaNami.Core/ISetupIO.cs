namespace SemaNami.Core;

// Abstracts console prompting so SetupWizard's orchestration logic is unit-testable.
public interface ISetupIO
{
    void WriteLine(string text);
    string? ReadLine();
}
