using QuestReader.Services;
using System.CommandLine;

var command = new RootCommand
{
    Name = "quest-parser",
    Description = "A tool to archive quest threads",
    TreatUnmatchedTokensAsErrors = true
};
var questNameArg = new Argument<string>(
    "questName",
    "Quest name to use for loading files and generating"
);
command.AddArgument(questNameArg);

command.SetHandler((string questName) => {
    var generator = new Generator(questName);
    generator.Run();
}, questNameArg);

return command.Invoke(args);
