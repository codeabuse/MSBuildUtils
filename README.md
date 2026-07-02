
A helper task to fix missing dependency mappings when NetBeauty2
is applied with shared runtime mode is enabled.

For more info, read my issue report and discussion: https://github.com/nulastudio/NetBeauty2/issues/92.

NetBeauty2 with SharedRuntimeMode puts all DLLs in a separate folder (with a structure like
 `.\%assembly_name%.dll\%random_guid%\%assembly_name%.dll`), and uses libloader to load them. The paths
to DLLs are written to the `%project_name%.runtimeconfig.json` in the output folder, but NBeauty may miss
some indirect dependencies, i.e. a package that is a dependency of another package that is a dependency
of another assembly.

(A workaround with static dependency list does not truly work, since the rebuilding of a dependency assembly
will generate new guid, tht is a subfolder name.)

This task collects the full dependency list of a target project from project.assets.json
in obj folder and updates the mapping in `*.runtimeconfig.json`.

.csproj usage:
```
<UsingTask TaskName="AddMissingNBeautyMappings"
           AssemblyFile="$(OutDir)..\Tools\MSBuildUtils\MSBuildUtils.dll"
           TaskFactory="TaskHostFactory"
           Runtime="CurrentRuntime" />

<Target Name="FixNBeautyDependencyMapping"
        Condition="'$(DisableBeauty)' == 'False' And ('$(BeautySharedRuntimeMode)' == 'True' Or '$(BeautySharedRuntimeMode)' == '--srmode')"
        AfterTargets="NetBeautyOnBuild_Fx">
    <AddMissingNBeautyMappings MappingsFilePath="$(OutDir)$(MSBuildProjectName).runtimeconfig.json"
                                ScanDirectory="$(BaseOutputPath)SharedLibs\"
                                ProjectAssetsFile="$(BaseIntermediateOutputPath)project.assets.json" />
</Target>
```
