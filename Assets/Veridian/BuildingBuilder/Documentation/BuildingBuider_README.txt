README: Building Builder - Low-Poly Architecture

Thank you for downloading the Building Builder asset. This tool is designed to help you quickly generate procedural low-poly architecture directly in the Unity Editor or at runtime.

Below is a quick guide to help you get started, understand the file structure, and find additional documentation.


Getting Started
To create your first building, follow these steps:

Open the generator window by navigating to Tools > Procedural Building Factory in the top menu bar.

In your Project window, locate either the demofamily or demobuilder profile (or create a new one via Right-Click > Create > Procedural).

Drag and drop the profile into the "Building Source" slot at the top of the editor window.

A temporary preview of the building will spawn in your scene at the origin coordinates (0,0,0). You can move this preview to a better location using the position fields in the window or the standard Unity transform handles.

If you are using a family profile, you can click the re-roll button to cycle through random variants. You can also modify the parameters on the profile itself and watch the preview update in real-time.

Once you are happy with the shape, click "Bake Prefab to Scene" to save it as a permanent asset in your project.


Assembly Structure
To keep your project clean and optimized, the code for this asset is divided into three distinct assemblies:

Core Assembly: Contains the primary generation scripts, data structures, and mathematical logic.

Editor Assembly: Contains the custom editor window and baking tools. This assembly references the Core assembly and is automatically stripped from your final game builds.

Components Assembly: Contains the runtime MonoBehaviours, such as the memory tracker and the runtime demo script. This assembly references the Core assembly and handles in-game generation events.


Full Documentation
A comprehensive PDF manual is included in the project files, detailing the parameters, editor workflow, and runtime API.

If you prefer to delete the PDF from your project to save space, the full documentation is also hosted online and can be accessed here:
https://docs.google.com/document/d/1xmieNUOBaVPZBor7rdfPqUytvuQBm4wvgQ07nR8j5Ys/edit?usp=sharing


Support and Contact
If you have a question about how to use the asset, or if you encounter a bug, please feel free to reach out to me at:
trevor.keiber@gmail.com

Please keep in mind that because this is a free asset, I cannot provide the same guarantee of dedicated support or custom modifications that you might expect from a paid asset. However, I will try my best to get back to you and address any issues.


Support the Developer
If you find this tool useful for your project, please consider leaving a rating and a comment on the Asset Store. Feedback from the community is highly appreciated and helps the asset gain visibility.

Additionally, I encourage you to check out my other tools on the Asset Store. If you need to map out larger environments, take a look at the free Procedural Town Generator. In fact, the entire layout of the sample town in the demo scene of this asset was automated in just a few seconds using the town generator. I also have several performance optimization tools available on my publisher page that pair well with these procedural assets.

Good luck with your project.