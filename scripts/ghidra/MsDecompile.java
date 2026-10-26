// Labels GameAssembly.dll with method names from scripts/RvaDump's TSV and decompiles the requested functions.
// Adapted from LordAndMaidenWork's LamDecompile.java (which read Il2CppDumper's script.json; Il2CppDumper cannot
// read this game's metadata v108). Run headless by scripts/decompile.py; the analysed project is opened read-only.
//
// Args: <names.tsv> <targets.txt> <outdir>
//   names.tsv: "0x<RVA>\t<name>[ | <alias>...]" per line.
//   targets.txt: one RVA per line (0x...). For each, writes <outdir>/<RVA>.c, or <outdir>/<RVA>.error with the reason.
//@category MoonSweepers

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;

import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.symbol.SourceType;

public class MsDecompile extends GhidraScript {

    private Address base;

    @Override
    public void run() throws Exception {
        String[] args = getScriptArgs();
        if (args.length != 3) {
            throw new IllegalArgumentException("usage: MsDecompile <names.tsv> <targets.txt> <outdir>");
        }
        base = currentProgram.getImageBase();

        int labelled = 0;
        for (String line : Files.readAllLines(Path.of(args[0]), StandardCharsets.UTF_8)) {
            int tab = line.indexOf('\t');
            if (tab < 0) {
                continue;
            }
            Address address = base.add(Long.decode(line.substring(0, tab)));
            String name = line.substring(tab + 1).split(" \\| ")[0];
            createLabel(address, name.replaceAll("[\\s,<>`]", "_"), true, SourceType.USER_DEFINED);
            labelled++;
        }
        println("MS labels: " + labelled);

        Path out = Path.of(args[2]);
        Files.createDirectories(out);
        List<String> targets = Files.readAllLines(Path.of(args[1]), StandardCharsets.UTF_8);
        DecompInterface decompiler = new DecompInterface();
        if (!decompiler.openProgram(currentProgram)) {
            throw new IllegalStateException("decompiler failed to open the program: " + decompiler.getLastMessage());
        }
        int ok = 0;
        int failed = 0;
        try {
            for (String line : targets) {
                String rva = line.trim();
                if (rva.isEmpty()) {
                    continue;
                }
                String error = decompileOne(decompiler, rva, out);
                if (error == null) {
                    ok++;
                } else {
                    failed++;
                    Files.writeString(out.resolve(rva + ".error"), error + "\n", StandardCharsets.UTF_8);
                }
            }
        } finally {
            decompiler.dispose();
        }
        println("MS decompiled: " + ok + " ok, " + failed + " failed -> " + out);
    }

    private String decompileOne(DecompInterface decompiler, String rva, Path out) throws Exception {
        Address address = base.add(Long.decode(rva));
        Function function = getFunctionAt(address);
        if (function == null) {
            function = createFunction(address, null);
        }
        if (function == null) {
            return "no function could be created at " + address;
        }
        DecompileResults result = decompiler.decompileFunction(function, 180, monitor);
        if (!result.decompileCompleted() || result.getDecompiledFunction() == null) {
            return "decompile failed: " + result.getErrorMessage();
        }
        String header = "// " + function.getName() + " @ " + address + " (RVA " + rva + ")\n";
        Files.writeString(out.resolve(rva + ".c"), header + result.getDecompiledFunction().getC(),
                StandardCharsets.UTF_8);
        return null;
    }
}
