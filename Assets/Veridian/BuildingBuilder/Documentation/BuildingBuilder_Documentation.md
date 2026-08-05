

**Part 1: Overview and Expectations**

Thank you for downloading the Procedural Building Builder. This asset is designed to help you generate low-poly buildings directly in the Unity Editor or at runtime using a profile-based parameter system.

Before getting started with the tool itself, there are a few important details to cover regarding compatibility, system limitations, and how to get the most out of your generated assets.

## **Compatibility and Support**

This asset requires Unity 6 or newer. It was developed with modern render pipelines in mind and might not be compatible with the built-in legacy render pipeline.

Please note that because this is a completely free asset, I cannot provide the same level of dedicated support or custom modifications for specific use cases that you might expect from a paid asset. The tool has been tested and is expected to work correctly within its intended scope. However, if you encounter bugs or have trouble using the core features, I would still appreciate hearing about your experience so I can look into potential fixes.

## **System Limitations**

It is important to understand the constraints of this tool from the start so you can determine if it fits your project.

* **Complexity:** This system cannot generate infinitely complex architecture. It uses a specific profile structure to create standard geometric variations, which we will cover in the technical sections.  
* **Detail Level:** The resulting buildings are inherently low-poly. They do not have interiors or working doors. The script only generates primitive box colliders for the foundation, main body, and roof. Characters will not be able to walk inside or interact closely with the structures.  
* **Material Optimization:** While the buildings do generate with LODs, they are not strictly optimized from a material standpoint. Each building uses multiple material slots. To properly optimize these buildings for performance in a larger game scene, you should consider using a separate tool to combine and atlas the materials so they share a single texture. That process is outside the scope of this generator.

## **Recommended Tools and Cross-Promotion**

If you are using this tool to populate a game world, there are several other assets I have created that you might find useful for your workflow.

* **Procedural Towns (Free):** I made a complimentary asset around the same time as this one, which is also free on my publisher page. It generates procedural town layouts and was designed specifically to work in tandem with this building generator to construct entire neighborhoods.  
* **Imposter Cross Tree Creator (Free):** The flat imposter trees you will see in the demo scene were made using this tool. It is another free asset of mine that can help you with rendering optimization.  
* **BurstLOD:** If you need to generate LODs for other meshes, I have an asset called BurstLOD that creates runtime or editor-based LODs at extremely fast speeds.

You can find these, along with a few other optimization assets, by visiting my publisher page.

Finally, if you like this asset and find it useful for your project, please consider taking a moment to leave a rating and a review on the Asset Store page. It helps me out quite a bit.

**Part 2: Getting Started and the Demo Scene**

The Procedural Building Builder was developed to quickly populate background scenery and mid-ground environments. It is best utilized in scenarios like city builders, flight simulators, or any project that requires a large volume of structural variety without the overhead of manually modeling dozens of unique houses.

To understand how the asset functions, it is recommended that you first open the included demo scene.

## **Exploring the Demo**

The demo scene contains a collection of sample buildings arranged into a small town layout. These pre-generated structures utilize the textures and materials included in the demo content folder, providing a clear look at how the generator can be applied to create varied neighborhood blocks.

In addition to the static town layout, the scene features an active example of the system's runtime generation capabilities. There is a GameObject running a MonoBehaviour script that continuously progresses a single building through various random architectural cycles. This serves as a practical demonstration of how the system handles dynamic mesh generation and memory cleanup during gameplay.

## **Pipeline Setup and Materials**

The materials provided within the demo scene are configured for the Universal Render Pipeline (URP), specifically using Simple Lit shaders. If your project uses the High Definition Render Pipeline (HDRP), the demo materials will not render correctly out of the box. You will need to manually change the shader on the demo materials to an HDRP-compatible equivalent for them to display properly.

## **Safe Deletion of Demo Assets**

While it is highly suggested that you take a look at the demo scene to see how the profiles and prefabs are set up, the demo content is entirely optional.

Once you understand the workflow, you can safely delete the entire demo folder, including the sample town, generated prefabs, and demo textures. Removing these files will not break the generator or affect the core scripts required to build your own structures.

**Part 3: Editor Workflow and Creating Buildings**

This section covers the step-by-step process of constructing buildings inside the Unity Editor. The editor window provides a visual, non-destructive way to craft individual buildings or cycle through thousands of random permutations before committing them to your project files.

## **Opening the Tool and Setting Up Profiles**

To begin, open the generator by navigating to **Tools \> Procedural Building Builder** in the top menu bar. This will open a custom editor window.

The tool requires a data source to know what kind of building to generate. This data is stored in ScriptableObjects. You can create these by right-clicking in your Project window, navigating to **Create \> Procedural**, and selecting either a Building Profile or a Building Family Profile.

* **Building Profile:** This is a static, deterministic profile. The values you set here directly represent a single, specific building configuration.  
* **Building Family Profile:** This is a dynamic profile that uses ranges and weighted probabilities. It defines an architectural style rather than a single building. When used, the generator will roll random values within your defined ranges to create unique variations.

Drag and drop your created profile into the "Building Source" field at the top of the editor window.

## **The Live Preview System**

Once a valid profile is assigned, the system will immediately generate a temporary preview of the building in your active scene.

You can move and rotate this preview using the position and rotation fields in the editor window, or by using the standard transform handles directly in the scene view. As you adjust the parameters in your profile, the preview mesh will update in real-time to reflect those changes.

If you are using a Building Family Profile, a "Re-roll Variant" button will appear in the window. Clicking this will generate a completely new permutation based on the probabilities you defined, allowing you to quickly review different procedural outcomes.

## **The Baking Process**

The preview building is entirely temporary. It is dynamically generated and flagged to not save with your scene. To convert this temporary preview into a usable game asset, you must bake it.

When you are satisfied with the preview, click the **Bake Prefab to Scene** button. You will be prompted to choose a save location, which defaults to an `Assets/Buildings/` directory.

The baking process does a few specific things automatically:

1. It creates a permanent, static prefab of the building in your project.  
2. It generates a dedicated companion folder right next to your new prefab.  
3. It extracts the procedural meshes and any generated materials into this folder as permanent `.asset` and `.mat` files.

This extraction is necessary because Unity cannot natively save procedurally generated meshes directly inside a prefab file. By organizing them into a dedicated sub-folder, it keeps your project directory clean and prevents asset bloat.

If you make a mistake, an "Undo Last Bake" button will appear, which safely deletes the prefab and its associated generated assets.

## **Material Best Practices**

The profiles contain slots for you to assign materials for the walls, roof, glass, masonry, and other components. If you leave these blank, the system will use plain, mono-color fallback materials.

It is highly advised that you do not rely on these default fallback materials when baking buildings for your final project. Because the generator extracts missing materials into new asset files during the bake process, using the defaults will result in the creation of duplicate, identical material files for every single building you bake. This is extremely wasteful.

Instead, you should assign your own materials in the profile before baking. The system will recognize persistent project materials and reference them correctly without duplicating them.

For the best visual results, use materials with textures that tile seamlessly. The generator includes a texture scale parameter to help fit tiling textures onto the geometry.

Alternatively, if you have a texture image of a complete building side that already includes drawn-on windows, doors, and trim, it is highly optimal to simply disable the procedural windows, doors, and trim in the profile entirely. You can then apply your facade image as the wall material. This saves geometry and utilizes your custom art directly.

**Part 4: Architectural Features and Parameters**

The generation system is built on a profile of parameters that dictate the physical layout and details of the building. This section breaks down how the generator interprets these parameters and the structural constraints of the system.

## **Footprint Constraints and Shapes**

The generator uses a strict orthogonal layout system. It cannot generate curved walls, cylinders, or arbitrary diagonal angles. However, by utilizing the wing parameters, you can create a wide variety of standard architectural footprints.

* **The Main Body:** Every building starts as a base rectangle defined by the standard Width and Length parameters.  
* **Wings (L and T Shapes):** Enabling the `Add Wing` parameter extrudes a perpendicular section from the side of the main building. The `Wing Offset` parameter dictates where this wing attaches. Setting the offset to \-1 or 1 makes it flush with the back or front, creating an L-shape. Setting it to 0 centers it, creating a T-shape.  
* **Mirrored Buildings (U and H Shapes):** Enabling the `Add Mirrored Building` parameter adds a secondary main body to the far end of the wing, running parallel to the original main body. By adjusting the `Mirrored Offset` parameter, you can shift this secondary building to create U-shapes, H-shapes, or staggered configurations.

## **Parameter Breakdown**

The profiles contain several categories of variables that control the final mesh generation.

### **Structure and Dimensions**

* **Width, Length, and Wall Height:** These control the core dimensions of the main rectangular body.  
* **Floor Count:** Determines how many vertical stories the building has. This directly multiplies the total wall height and dictates how many rows of windows are generated.

### **Foundation and Masonry**

* **Add Foundation:** Generates a solid block beneath the building. The `Foundation Extension` parameter allows you to make this block wider and deeper than the main walls to create a visible ledge.  
* **Add Chimney:** Generates a simple chimney structure. If the roof is gabled, the chimney height will automatically adjust to clear the slope of the roof.

### **Roof Generation**

* **Roof Type:** You can select between Flat and Gabled roofs.  
* **Roof Height and Overhang:** Controls the vertical peak of the roof and how far the eaves extend past the walls. If you are using a Gabled roof with wings or mirrored buildings, the system will automatically calculate the intersecting valley angles to ensure a continuous, enclosed roof mesh.

### **Windows and Doors**

Windows and doors are generated as floating planar decals that sit slightly off the surface of the walls.

* **Distribution Counts:** You specify the target number of windows for each cardinal facade (Front, Back, Left, Right). The system will automatically calculate the even spacing required to fit them. If the wall is too short to physically fit the requested number of windows based on the window width, the generator will cap the count to prevent them from overlapping.  
* **Doors:** You can toggle doors for the front, back, and wing sections. The `Door Offset From Center` allows you to slide the door left or right along its respective wall. The system will detect the door's position and automatically remove any conflicting windows on the ground floor.  
* **Detail Offset Bias:** This is a small but critical technical parameter. Because windows, doors, and frames are planar surfaces projected against the main wall, they can cause Z-fighting rendering artifacts. This offset pushes them outward by a fraction of a unit to ensure clean rendering.

### **Porches**

* **Porch Generation:** You can attach simple porch structures to the front, back, or wing of the building. These consist of a roof, a foundation extension, and physical 3D columns. The width, depth, and height of the porch are independently adjustable.

### **Accents and Trims**

* **Trim Options:** You can toggle corner, horizontal, vertical, and fascia trims. Similar to windows, these are generated as planar decals relying on the Detail Offset Bias.  
* Horizontal trims are placed between floor levels, while vertical trims attempt to space themselves evenly between windows on wider facades. Fascia trims add a border along the edge of the roof eaves.

**Part 5: Editor Scripting API**

If you prefer to generate buildings through your own custom tools or editor scripts rather than using the provided interface, you can access the core generation logic directly. The architecture separates the mathematical mesh generation from the Unity GameObject assembly, allowing you to easily trigger generation sequences via C\# scripts within the editor.

## **Core API Classes**

To generate a building through code, you will interact primarily with two classes located in the `Veridian.BuildingSystem.Runtime` namespace.

* **BuildingBuilder:** This class handles all the procedural mathematics, footprint tracing, and vertex plotting. Calling its `Generate()` method with a profile returns an array of `MeshData` objects, which represent the main mesh and its LODs.  
* **BuildingFactory:** This static class handles the Unity-specific assembly. The `CreateBuilding` method takes your profile and the generated `MeshData` array, returning a fully constructed GameObject complete with MeshFilters, MeshRenderers, and colliders. This specific method skips the memory tracking dependencies required for runtime generation, making it the correct choice for editor-side automation.

## **Automating Generation via Script**

Below is a standard example of how you might write a custom editor script to generate a building and place it into your scene.

C\#  
using UnityEngine;  
using UnityEditor;  
using Veridian.BuildingSystem.Runtime;

public class CustomBuildingAutomator  
{  
    // A sample method demonstrating how to generate a building via script  
    public static GameObject GenerateCustomBuilding(BuildingProfile profile, string buildingName)  
    {  
        // 1\. Initialize the builder engine  
        BuildingBuilder builder \= new BuildingBuilder();

        // 2\. Generate the raw mathematical mesh data based on the profile  
        BuildingBuilder.MeshData\[\] meshDataArray \= builder.Generate(profile);

        // 3\. Assemble the Unity GameObject using the Editor-safe factory method  
        GameObject newBuilding \= BuildingFactory.CreateBuilding(profile, meshDataArray, buildingName);

        // At this point, the building exists in the scene hierarchy.  
        // You can use standard Unity Editor APIs to save it as a prefab or extract its meshes.  
          
        // Example of saving to a prefab (requires appropriate folder paths and mesh saving logic):  
        // string localPath \= "Assets/Prefabs/" \+ buildingName \+ ".prefab";  
        // PrefabUtility.SaveAsPrefabAssetAndConnect(newBuilding, localPath, InteractionMode.UserAction, out bool success);

        return newBuilding;  
    }  
}

## **Potential Use Cases**

Accessing the generation API directly is useful if you are building your own internal pipeline tools.

For example, if you are developing a procedural city builder, you could write a script that instantiates a `BuildingFamilyProfile`, loops through it 50 times using the `GenerateRandomProfile()` method, and feeds the results into the `BuildingFactory`. This would allow you to mass-generate an entire library of unique building prefabs with a single button click. You could also tie this logic into a spline-based placement tool to automatically construct buildings along a custom road network.

**Part 6: Runtime Generation and Memory Management**

Generating buildings during active gameplay requires a different architectural approach than baking prefabs in the editor. When you generate custom meshes and cloned materials at runtime, Unity treats them as unmanaged memory. If you simply destroy the building's GameObject, those raw meshes and materials will remain orphaned in your RAM and GPU, quickly causing a memory leak.

The runtime generation API is specifically designed to safely package these unmanaged assets so they can be securely tracked and destroyed.

## **The Runtime Generation API**

To generate a building during gameplay, you will use the `BuildingFactory.GenerateAtRuntime` method. Unlike the editor equivalent, this method accepts two optional list parameters designed to catch the raw generated data.

When calling this method, you pass in an empty list for meshes and an empty list for materials. The factory will populate these lists with every procedural mesh and fallback material it creates during the assembly of that specific building.

C\#  
// Prepare lists to catch the unmanaged assets  
List\<Mesh\> generatedMeshes \= new List\<Mesh\>();  
List\<Material\> generatedMaterials \= new List\<Material\>();

// Generate the physical building and extract the asset references  
GameObject myBuilding \= BuildingFactory.GenerateAtRuntime(myProfile, generatedMeshes, generatedMaterials);

## **The Runtime Building Tracker**

Catching the assets is only the first step. You must then ensure they are destroyed when the building is no longer needed. The system includes a component called `RuntimeBuildingTracker` to automate this process.

Immediately after generating a building, you should add this component to the root GameObject and feed it the lists you just populated.

C\#  
RuntimeBuildingTracker tracker \= myBuilding.AddComponent\<RuntimeBuildingTracker\>();

foreach (Mesh mesh in generatedMeshes)   
{  
    tracker.TrackMesh(mesh);  
}  
foreach (Material mat in generatedMaterials)   
{  
    tracker.TrackMaterial(mat);  
}

By doing this, you shift the responsibility of memory management to the tracker. When you eventually call `Destroy(myBuilding)`, the tracker intercepts the destruction event and natively flushes all the tracked unmanaged meshes and fallback materials from memory alongside the GameObject.

## **The Demo Runtime Script**

The included `RuntimeBuildingDemo.cs` script serves as a complete, practical example of how to implement an endless, memory-safe generation loop. It uses a Coroutine to continuously roll random variations from a Building Family Profile, spawn them, and then safely destroy them after a set interval.

If you are building a system that cycles through structures, it is highly recommended that you review the `CleanupCurrentBuilding()` method within this demo script.

### **Handling Dynamic Profiles**

The demo script highlights one additional, critical rule regarding memory management: handling temporary ScriptableObjects.

When you call `familyProfile.GenerateRandomProfile()`, the engine uses `ScriptableObject.CreateInstance` to build a temporary, dynamic Building Profile in memory based on your randomized weights. ScriptableObjects created this way bypass standard Unity Garbage Collection. They will persist in memory indefinitely unless manually removed.

The demo script demonstrates the correct cleanup sequence at the end of every cycle:

C\#  
private void CleanupCurrentBuilding()  
{  
    // 1\. Destroy the physical building. The attached tracker handles the meshes/materials.  
    if (currentBuilding \!= null)  
    {  
        Destroy(currentBuilding);  
        currentBuilding \= null;  
    }

    // 2\. Destroy the dynamically instantiated ScriptableObject profile to prevent a memory leak.  
    if (currentDynamicProfile \!= null)  
    {  
        Destroy(currentDynamicProfile);  
        currentDynamicProfile \= null;  
    }  
}

If your game relies on generating random profiles on the fly rather than using static, pre-made profiles, you must ensure you destroy the temporary profile object once the building has been generated and the data is no longer needed.

**Part 7: Final Thoughts and Additional Resources**

Thank you again for downloading the Procedural Building Factory. I hope this documentation has provided a clear understanding of how to use the generator effectively, whether you are baking static prefabs in the editor or spawning dynamic architecture during gameplay.

As a reminder, this tool is best utilized for mid-ground and background structures. If you plan to use a large number of these buildings in a dense scene, remember to utilize a separate tool to atlas and combine your materials after generation to ensure your draw calls remain as optimized as possible.

If you are looking to expand your project's environments further, please take a moment to visit my publisher page. You will find the free Procedural Towns asset there, which was built alongside this generator to help you map out complete neighborhood grids that these buildings can snap into. You can also find the free Imposter Cross Tree Creator and my BurstLOD asset, both of which are designed to help you maintain strict performance budgets when rendering expansive environments.

Finally, if this tool has been a helpful addition to your development workflow, leaving a rating and review on the Asset Store page is highly appreciated. It helps the asset gain visibility and supports the continued creation of these free tools.

Good luck with your project.

