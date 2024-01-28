
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;


class Program
{

    static async Task<int> Main(string[] args)
    {

        var rootCommand = new RootCommand();

        var bundleCommand = new Command("bundle", "Bundle code files into a single file");

        var languageOption = new Option<List<string>>(
            "--language",
            "Programming languages to include in the bundle"
        )
        { IsRequired = true };



        var outputOption = new Option<FileInfo>(
            "--output",
            "File path and name for the bundled output"
        )
        { IsRequired = true };



        var noteOption = new Option<bool>(
            "--note",
            "Include source code comments in the bundled file"
        );


        var sortOption = new Option<string>(
            "--sort",
            description: "Sort the bundled files alphabetically by file name or by code type",
            getDefaultValue: () => "name"
        );


        var removeEmptyLinesOption = new Option<bool>(
            "--remove-empty-lines",
            "Remove empty lines from the bundled file"
        );


        var authorOption = new Option<string>(
            "--author",
            "Name of the file creator to be noted at the top of the bundle file"
        );
        outputOption.AddAlias("-o");
        languageOption.AddAlias("-l");
        noteOption.AddAlias("-n");
        sortOption.AddAlias("-s");
        removeEmptyLinesOption.AddAlias("-re");
        authorOption.AddAlias("-a");

        bundleCommand.AddOption(languageOption);
        bundleCommand.AddOption(outputOption);
        bundleCommand.AddOption(noteOption);
        bundleCommand.AddOption(sortOption);
        bundleCommand.AddOption(removeEmptyLinesOption);
        bundleCommand.AddOption(authorOption);

        bundleCommand.Handler = CommandHandler.Create<FileInfo, List<string>, bool, string, bool, string>(async (output, languages, note, sort, removeEmptyLines, author) =>
        {
            await BundleFiles(output, languages, note, sort, removeEmptyLines, author);
        });
        var createRspCommand = new Command("create-rsp", "Create a response file for the bundle command");
        createRspCommand.Handler = CommandHandler.Create(CreateRspFile);

        rootCommand.AddCommand(bundleCommand);
        rootCommand.AddCommand(createRspCommand);

        return await rootCommand.InvokeAsync(args);
    }


    private static int CreateRspFile()
    {
        try
        {
            Console.WriteLine("Please provide the following information for the create-rsp command:");

            // Get output file path
            Console.Write("Output file path and name: ");
            string outputPath = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                Console.WriteLine("Error: Output file path cannot be empty.");
                return 1;
            }

            // Validate and sanitize output file path
            outputPath = outputPath.Trim();
            if (outputPath.Contains(" "))
            {
                Console.WriteLine("Error: Output file path cannot contain spaces.");
                return 1;
            }

            // Check if output file already exists
            if (File.Exists(outputPath))
            {
                Console.WriteLine($"Error: Output file '{outputPath}' already exists. Choose a different name or location.");
                return 1;
            }

            // Get programming languages
            Console.Write("Programming languages (comma-separated): ");
            string languagesInput = Console.ReadLine();
            List<string> languages = languagesInput.Split(',').Select(lang => lang.Trim()).ToList();
            if (!languages.Any())
            {
                Console.WriteLine("Error: At least one programming language must be specified.");
                return 1;
            }

            //  Get additional options
            Console.Write("Include source code comments? (Y/N): ");
            bool note = Console.ReadLine()?.Trim().ToUpperInvariant() == "Y";

            Console.Write("Sort the bundled files (name/type): ");
            string sort = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (sort != "name" && sort != "type")
            {
                Console.WriteLine("Error: Invalid sort option. Please specify 'name' or 'type'.");
                return 1;
            }

            Console.Write("Remove empty lines from the bundled file? (Y/N): ");
            bool removeEmptyLines = Console.ReadLine()?.Trim().ToUpperInvariant() == "Y";

            Console.Write("Author name to be noted at the top of the bundle file (optional): ");
            string author = Console.ReadLine()?.Trim();

            // Build response file content
            var rspContent = $"bundle --output {outputPath} " +
                   $"{string.Join(" ", languages.Select(lang => $"--language {lang}"))} " +
                   $"{(note ? "--note" : "")} " +
                   $"--sort {sort} " +
                   $"{(removeEmptyLines ? "--remove-empty-lines" : "")} " +
                     $"{(string.IsNullOrEmpty(author) ? "" : $"--author {author}")}";

            // Save response file
            try
            {
                File.WriteAllText("bundle.rsp", rspContent);
                return 0; // Success
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return 1; // Error
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1; // General Error
        }
    }
    static async Task BundleFiles(FileInfo output, List<string> languages, bool note, string sort, bool removeEmptyLines, string author)
    {
        var PossibleExtentions = new string[] { "c#", "javascript", "python" };

        try
        {
            // Validate output file
            if (output == null)
            {
                Console.WriteLine("Error: Output file is required.");
                return;
            }
            // Check if output file already exists
            if (File.Exists(output.FullName))
            {
                Console.WriteLine($"Error: Output file '{output.FullName}' already exists. Choose a different name or location.");
                return;
            }
            languages = languages.Where(language => PossibleExtentions.Contains(language)).ToList();
            var extensionsToInclude = GetExtensionsFromLanguages(languages);
            var filesToBundle = Directory.GetFiles(".", "*.*", SearchOption.AllDirectories)
           
                .Where(file => IsInExcludedDirectory(file))
               //  .Where(file => ShouldIncludeFile(file, extensionsToInclude))// לא עובד
                .ToList();
           
            filesToBundle = SortFiles(filesToBundle, sort);
            using (var outputStream = new StreamWriter(output.FullName, append: false))
            {
                if (!string.IsNullOrWhiteSpace(author))
                {
                    await outputStream.WriteLineAsync($"// Author: {author}");
                }
                foreach (var file in filesToBundle)
                {
                    if (note) { await outputStream.WriteLineAsync($"// Source: {file}"); }
                    string content = await File.ReadAllTextAsync(file);
                    if (removeEmptyLines) { content = RemoveEmptyLines(content); }
                    await outputStream.WriteLineAsync(content);
                    await outputStream.WriteLineAsync();
                }
            }
            Console.WriteLine("Bundle created successfully.!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private static string RemoveEmptyLines(string content)
    {
        return string.Join(Environment.NewLine, content.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
    }

    private static List<string> GetExtensionsFromLanguages(List<string> languages)
    {
        if (languages.Contains("all", StringComparer.OrdinalIgnoreCase))
        {
            return new List<string> { ".cs", ".js", ".py" };
        }

        var languageExtensions = new Dictionary<string, string>
        {
            ["c#"] = ".cs",
            ["javascript"] = ".js",
            ["python"] = ".py"
        };

        return languages
            .Where(language => languageExtensions.ContainsKey(language.ToLower()))
            .Select(language => languageExtensions[language.ToLower()])
            .ToList();
    }
    private static List<string> SortFiles(List<string> files, string sort)
    {
        switch (sort.ToLower())
        {
            case "name":
                return files.OrderBy(Path.GetFileName).ToList();
            case "type":
                return files.OrderBy(Path.GetExtension).ToList();
            default:
                return files;
        }
    }
    private static bool ShouldIncludeFile(string filePath, List<string> extensionsToInclude)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extensionsToInclude.Contains(extension);
    }
    private static bool IsInExcludedDirectory(string file)
    {
        var excludedDirectories = new string[] { "bin", "debug", ".vs", "obj", "venv", ".idea" };
        return !excludedDirectories.Any(dir => file.Contains(Path.DirectorySeparatorChar + dir + Path.DirectorySeparatorChar));
    }
}