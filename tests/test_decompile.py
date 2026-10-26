from pathlib import Path

import pytest

from scripts import decompile

NAMES = (
    "0x100\tNs.A$$Run | Ns.B$$Run\n"
    "0x200\tNs.A$$Stop\n"
    "0x300\tNs.Other$$Run\n"
)


@pytest.fixture
def by_name(tmp_path: Path) -> dict[str, str]:
    path = tmp_path / "names.tsv"
    path.write_text(NAMES, encoding="utf-8")
    return decompile.load_names(path)


def test_load_names_maps_folded_aliases_to_shared_address(by_name):
    assert by_name["Ns.B$$Run"] == "0x100"


def test_resolve_type_selects_only_that_types_methods(by_name):
    assert decompile.resolve([], ["Ns.A"], by_name) == {"Ns.A$$Run": "0x100", "Ns.A$$Stop": "0x200"}


def test_resolve_rejects_unknown_method(by_name):
    with pytest.raises(decompile.DecompileError, match="no code address"):
        decompile.resolve(["Ns.A$$Missing"], [], by_name)


def test_resolve_rejects_type_without_methods(by_name):
    with pytest.raises(decompile.DecompileError, match="no methods"):
        decompile.resolve([], ["Ns.Nothing"], by_name)


def test_load_names_requires_the_table(tmp_path):
    with pytest.raises(decompile.DecompileError, match="RvaDump"):
        decompile.load_names(tmp_path / "absent.tsv")


def test_safe_file_name_keeps_method_separator():
    assert decompile.safe_file_name("Ns.A<T>$$Run") == "Ns.A_T_$$Run"
