# UEFI BUILD1B Artifact Provenance Reconciliation

**Outcome C — BUILD1B_BLOCKED.** The auditor now describes the observed hash relationships, but no source or build-input fingerprint binds the existing records and staged binaries to an authoritative Phase 29–35 build. The 40 mismatches cannot be safely repaired by choosing either the recorded outputs or staged files. No artifacts were changed or rebuilt.

Inspection date: 2026-10-28. All filesystem inspection and genuine artifact checks were read-only. Code, tests, and this report were changed only in `D:\dev\guideXOSUEFI_BUILD_INFRA`.

## 1. Baseline and scope

BUILD_INFRA started on branch `BUILD_INFRA` at `2b66faefa501dc681efce15e22100098b5e5ca52`, tracking `origin/BUILD_INFRA` and already ahead by one commit. Its worktree was clean before this task. Mainline, APP_MODEL, and MODERN_INPUT were inspected without changing branches or files. At inspection, mainline was at `7e57a5e666d219a4fcf44476dbf954e950ed3549` with pre-existing untracked ISO and test build outputs; APP_MODEL was at `9c773d55d2b8ce9203b4b8e1b08018014b8f88c7`; MODERN_INPUT was at `1ec202add2754665e681878582d85177c236d306` with pre-existing local edits and untracked artifacts. Those existing files were left untouched.

The read-only staging audit found all 41 EXE/GXMI pairs present and the complete pair-set signature identical across BUILD_INFRA, mainline, APP_MODEL, and MODERN_INPUT:

| Checkout | Complete pairs | Pair-set SHA-256 |
|---|---:|---|
| guideXOSUEFI_BUILD_INFRA | 41/41 | 4A58BB07AFB87523358708181859160C5C9B08032A7BC135CE270F6436161582 |
| guideXOSUEFI | 41/41 | 4A58BB07AFB87523358708181859160C5C9B08032A7BC135CE270F6436161582 |
| guideXOSUEFI_APP_MODEL | 41/41 | 4A58BB07AFB87523358708181859160C5C9B08032A7BC135CE270F6436161582 |
| guideXOSUEFI_MODERN_INPUT | 41/41 | 4A58BB07AFB87523358708181859160C5C9B08032A7BC135CE270F6436161582 |

The signature proves byte equality among the current checkouts. It does not prove who built those bytes or which source revision they represent.

## 2. Genuine artifact audit

The BUILD1B auditor was run with APP_MODEL as the receiving checkout and mainline as the candidate. The mainline artifact records and current output files were read directly from their recorded paths. The staged EXEs and GXMI descriptors were hashed in place. The audit did not generate identities or alter files.

Across the mainline records, all 41 have the expected schema, producer, `guidexos-x64` target, `guidexos` target OS, variant, project, output path, length, and SHA-256 fields. The records do not contain an immutable source commit or a SHA-256 build-input/toolchain fingerprint. The native generated identity source currently matches the 41 staged executable hashes, and all 41 GXMI descriptors validate their format, flags, executable length, and embedded executable digest. Those facts establish current byte relationships, not source provenance.

The native managed-identity generator was also run in `--audit-only` mode against mainline with its existing identity source. It rejected the first mismatch, Phase29 `success`: “Staged executable differs byte-for-byte from final build output.” The generator's checks were not weakened.

## 3. Complete 41-row hash classification

Legend: **E1** = record hash equals current output hash, but differs from staged EXE; **E2** = record hash equals staged EXE, but differs from the file now at the recorded output path; **M** = record, current output, and staged EXE all match. For every row, the GXMI embedded digest equals the staged EXE hash; the separate GXMI-file SHA-256 and descriptor flags are shown. No row has complete source provenance because the records omit immutable source and input fingerprints.

| Phase / variant | Class | Record SHA-256 | Current output SHA-256 | Staged EXE / GXMI embedded SHA-256 | GXMI file SHA-256 | Flags | Stage-hash output found? |
|---|---|---|---|---|---|---|---|
| Phase29 / success | E1 | F95857C741553ED0E951EC48D5642C164FFE60B8FD3751375BD715F1A15ADFBF | F95857C741553ED0E951EC48D5642C164FFE60B8FD3751375BD715F1A15ADFBF | 275C4F166F6964478A68D08E61B22BAB23507CF2B07F32C1BB7EE90EE52B0F56 | 5F02FB7EF7827639B0E1A27F97AF3BEAE9FC8399B5BB0649981958D78EEA0BA9 | 0x0000081F | no |
| Phase29 / title-failure | E1 | C4971145688466B7F59385F7738D38D34EBE46C3510FFA262FFBACD9717DB342 | C4971145688466B7F59385F7738D38D34EBE46C3510FFA262FFBACD9717DB342 | 0705F5990DD159FFE971768B795954899E7BEADD5F6FE7E5184422A03E70BC8D | 3BACCA009D71FACF9C2026151E2C38CB493939C6583DEB292B85679BEB6199C4 | 0x0000101F | no |
| Phase29 / body-failure | E1 | 0A6DBAA0CE747EC6CBFE42FB0741B7E2A3A6B756A6B8AD7B479764D3D14B28BC | 0A6DBAA0CE747EC6CBFE42FB0741B7E2A3A6B756A6B8AD7B479764D3D14B28BC | 16D7C0FA03520308AB2C85D19CE38D717E3B4C8CB664F0B273293AFE1A411C68 | AD187BAECFF210AA512C8D62696A82EA51A8B911D9310F40138011AE9DCAB1BE | 0x0000201F | no |
| Phase29 / invalid-type | E1 | 07140CD111FC7A467B8F6CC8DFD8D93320B246EAA76A078FA9F2DE4778337CE0 | 07140CD111FC7A467B8F6CC8DFD8D93320B246EAA76A078FA9F2DE4778337CE0 | BCF2D54F01707D08F6C0439606365CB4B9B9F58D28771D5E693E8D2AA3B68988 | 2372E27DD1CC82B549E6C507750BD48F9DA697D904FD0F96900DC411493ACF83 | 0x0000801F | no |
| Phase29 / failfast | E1 | E5B7301054D1393AC6F678C8F9243E69C4DB0A1E5F69DCDD95DDB665AFC300E9 | E5B7301054D1393AC6F678C8F9243E69C4DB0A1E5F69DCDD95DDB665AFC300E9 | 101606F5BD2786C851B7C9F5A8453E2F1871D89C910FB9DAFC3DBCBF70852C90 | ABD6F9FE082FA84368B79869962B643EB747199B5800A8D67253F2D25098B2E3 | 0x0000401F | no |
| Phase30 / success | E1 | 375834F8223C1F1339B71D6F2A7FEFEECB3A969251E05D9A2AA5AA24E4F90F6C | 375834F8223C1F1339B71D6F2A7FEFEECB3A969251E05D9A2AA5AA24E4F90F6C | AE14B6A02CD23C47788124DC8E089770D05B1928FDA3B7ACBB63103C9DE60CB2 | 6AC19F87E47C74DEC819A849416CEB5CBF92566AD0D9718B3A374486B32CDDA0 | 0x0001001F | no |
| Phase30 / reader | E1 | 1A06906BC2BE8B6BDD1F2314B0CD0AB9F8C6FD4E6F1033397046AAEC5C0F481B | 1A06906BC2BE8B6BDD1F2314B0CD0AB9F8C6FD4E6F1033397046AAEC5C0F481B | E2FED72AE90153B00ACB9FC116DA649EC46291A9DEC612F3A7724566FA4F2032 | D5762FF4819DC22F46B29EA3BC1B9A72DACD21A8E8635A9388BF6BB7E243BE3F | 0x0002001F | no |
| Phase30 / cross-writer | E1 | E5FFB468FB7F02F1B9A7EC1334CEFF6F8735134A844B05595BA5CB26741AC980 | E5FFB468FB7F02F1B9A7EC1334CEFF6F8735134A844B05595BA5CB26741AC980 | 5E88D66A5BBF99F61092504BEA9E2C0245FD9DA8701510B84A350B6736733EF4 | D20CB5B28EEF7A5D9A81F52638D5BE40197528BA679824287E6CA13F74030A69 | 0x0004001F | no |
| Phase30 / overwrite | E1 | 09B55A5BF5955944F41CB9CDEE7144A9294B8A7A9AECB80DC9BA1E98A200C2CD | 09B55A5BF5955944F41CB9CDEE7144A9294B8A7A9AECB80DC9BA1E98A200C2CD | BE433A99B2DE7D802370D993A6CB4E4FC513F2CD705B3A1B915384A655BE5321 | A385C520B434BE906646230B9A1C053D3E836161CA6DF905C806C3E780E22FEE | 0x0008001F | no |
| Phase30 / empty | E1 | 6482347871A4BF4D07801D671018126E781EADC0498A833454E91CF816E03C9A | 6482347871A4BF4D07801D671018126E781EADC0498A833454E91CF816E03C9A | E2B11596065F2BCFB725B7369DB472B3C2B801F6D68F3D076927EEA0A7DF3FFA | 71A1F12A8D0580CCDEAADE60DFE9A3DE112236CD54114AF4A735CFD694D35470 | 0x0010001F | no |
| Phase30 / clear | E1 | D2048D694F129179933295B9B4E8A927F2A9081F0CF6FD37415F4BCE9FCB7D73 | D2048D694F129179933295B9B4E8A927F2A9081F0CF6FD37415F4BCE9FCB7D73 | 7755C6B04DA86DB5A36D42D137ECF75536AC878610E4F0FC4D6DDC11DC88AB30 | 0B5550C8B5596C79AA58F96B24E4284F1FCF7C743235038B4C531400053364E2 | 0x0020001F | no |
| Phase30 / oversize | E1 | 8DA34634942F96EDA4090B577ABCB2D0D1D5C55F6B18829599A471782FD78D3D | 8DA34634942F96EDA4090B577ABCB2D0D1D5C55F6B18829599A471782FD78D3D | BF9C2618A0C1ED8255A4B786D64573BAD5DB90AEBC4F2DD17D92DB249AF3B10F | F4DD95F90E06932E258A69D21EA6E26EE014DA0A9B1D2B5421BD99FF4550E567 | 0x0040001F | no |
| Phase30 / malformed-length | E1 | CBA0AA7A35025AE0A9BDE913F55431432CD7B2A2DACEA365D3A54031B8A9AA43 | CBA0AA7A35025AE0A9BDE913F55431432CD7B2A2DACEA365D3A54031B8A9AA43 | AF42E775E1E2EB24E95893C219138D93ACDF4507E9CCBE71A924C31D222E2DCE | 7B1781AE0769075E1D03D1565E2CEE0058D6DB190FB54A9E80AD4F093885B3C2 | 0x0080001F | no |
| Phase30 / failfast | E1 | 4131068DAF4BE30D2F008EAB047F82BDC8ABEF46443CFD5FC3682C2E1A3F7664 | 4131068DAF4BE30D2F008EAB047F82BDC8ABEF46443CFD5FC3682C2E1A3F7664 | 1BF8001920FA3154391A1E685E2C73B922A384FE529330D9DC2C71CAC1A66ECC | 612911F2C63AAE3B09E9B63A7764B9674E1B656B8D108C3EBE1EB8D9F8062395 | 0x0100001F | no |
| Phase31 / success | E1 | B4878A48875E391E93B2C9B98E6234DBC49F7F97CB2C82423FDE99B42F764C04 | B4878A48875E391E93B2C9B98E6234DBC49F7F97CB2C82423FDE99B42F764C04 | 98CEBDE2F30D766CCF5B4B0CC3E263D63B4C74FFDBB5CE43AAC50E64F632F032 | 17B282A7AA13A1356684BBF0402AA44EF7C84A88370C9A45A0FC7511E1AE2FBF | 0x0200001F | no |
| Phase31 / invalid-target | E1 | 29A66B99D219D35C3B649238502D91FF4C0231B2167B3FEC397A10933F30F3D7 | 29A66B99D219D35C3B649238502D91FF4C0231B2167B3FEC397A10933F30F3D7 | 571592912B355DA3019478F0F944D7C807A699FC7CEA8ECDB90E0920FC9654F3 | C8AF627C4FFC9B7FDFB770BC52495F579742AF003AF93C2AE665EA9E8837C49F | 0x0400001F | no |
| Phase31 / oversize | E1 | 7155DDAE407D7009BB5DBC3DBEFD050C7E1F308FA9176C9A47E8350AF21124F8 | 7155DDAE407D7009BB5DBC3DBEFD050C7E1F308FA9176C9A47E8350AF21124F8 | 6886CB14043D760C0EE174E4E9AC45A257312F0D9E859C9F2E53A8105F977274 | C891E87EE232D81B2DD13F10DF7EC4AA2097F18091712D171D9726B2B673314B | 0x0800001F | no |
| Phase31 / failfast | E1 | 333A77DA80F251910C0CC6091C6D95B6BF53E13AF4FD6AA7F85DACD23934F667 | 333A77DA80F251910C0CC6091C6D95B6BF53E13AF4FD6AA7F85DACD23934F667 | 2B86CBB219900C5AFA4014D1C596809FE1E6CA2A12E15764FA8927F54DD72552 | 0AA187591DFF5107087D9F4695083830BD47F5AFAEE5F2B08312515B53C1CF00 | 0x1000001F | no |
| Phase31 / stale-owner | E1 | 1965CE35C604015BDCA728BC6F5745428DC8F47B183AC6377C4BD4A78743393E | 1965CE35C604015BDCA728BC6F5745428DC8F47B183AC6377C4BD4A78743393E | 377D660BD85186EC2D1A224163C311C79BE4B19F30CE64C522B0F5E3A4776554 | 307A11687E010D9E505A836EF2303076A7FE8A3F92BDD4D3E7015CCE06679F0D | 0x2000001F | no |
| Phase32 / success | E1 | ED66617404BFDFDABE7762C91B4959857887742270BC2B52DEDEB7CA54C79148 | ED66617404BFDFDABE7762C91B4959857887742270BC2B52DEDEB7CA54C79148 | 9527F420864678CE78BEA6121519F340B6A4DFB46499497534CD190A30A508EF | 90D8A335EDB10A6E1FD6C55ACF587BD878A3E934B765CE6A7214B549219EAADA | 0x4000001F | no |
| Phase32 / failfast | E1 | 0A4057CC4B636647A124796F46505D08A99E9A5491E6A826FAE009678A40B750 | 0A4057CC4B636647A124796F46505D08A99E9A5491E6A826FAE009678A40B750 | A68B02A313BF42B4517ABCECA9EA481DB5F8CD8025D1076F860841E4BFD650A7 | BE56C131407289E2FF0E8F2D5C71E442E4C1CFC229D1357D7F261D553E10A02C | 0x8000001F | no |
| Phase32 / stale-owner | E1 | 40E351583772B086FA3B18FFEE5A496BDBA33076DD6AED284CE831BE32F6EBD5 | 40E351583772B086FA3B18FFEE5A496BDBA33076DD6AED284CE831BE32F6EBD5 | E895270EBC7C66FFD532208F7638A233C9BF4AE3C287AE978C79FAA739359535 | 059C71837FD345209C306B19131C5B892D9EBAA84CA5857FF0ECD5C13F1892AB | 0xC000001F | no |
| Phase33 / success | E1 | 07C69ADB76089C1EDF30DC1B238377928D103BB97039CAD560B8D77F7F83D30A | 07C69ADB76089C1EDF30DC1B238377928D103BB97039CAD560B8D77F7F83D30A | 8E518AD58899230051336ADE76924414E38120818957B7801447D3E314F8197F | 4859BCD82252EC0AD079B3F0919E64E62B09F2DB41D5B5140A779CA0ECF9C971 | 0xC200001F | no |
| Phase33 / failfast | E1 | 42AC5498B225CA7652441075C2B8D17F3E1DF0E0930A5E3A1C83043F3DF90906 | 42AC5498B225CA7652441075C2B8D17F3E1DF0E0930A5E3A1C83043F3DF90906 | 9D4F926407B231B79B2B00B55CDF3F8D97A60BEB117B4AFD102562987A031BEC | C432E3CD5B9B778DB0955893C7C99EB0694BB397AA25CD31DBA4FA6DDF06AD74 | 0xC400001F | no |
| Phase33 / stale-owner | E1 | DAAF886D66BC7E9054FD6DF1A63F0E703F8FC60B4369CEAA1931FAB1F6A97373 | DAAF886D66BC7E9054FD6DF1A63F0E703F8FC60B4369CEAA1931FAB1F6A97373 | BADCCFBDB753FAAA7BDDF2743F2C9BD2A9342EDC3E35A60531A7A1E939801B94 | 91B8AA04D63FFE99A1FD9A2136C32FC614A8BEFE87C56B9A2DB1EE1061473DAB | 0xC800001F | no |
| Phase33 / invalid-action | E1 | A99F2626BD3A3AF65D0F4E9B26C0A6DBED932E21E432CD4153110D6915414F54 | A99F2626BD3A3AF65D0F4E9B26C0A6DBED932E21E432CD4153110D6915414F54 | 84B146A90FC54824DFFE20707467F4D149EC41DFF9A1CB54C0DA18BAA83B217E | F87681113E67446CD0432DCF010E540DBE5E9AD4EDB3371E5B85E14B2812DA22 | 0xD000001F | no |
| Phase33 / malformed | E1 | 601B288BB32D80A7BA389F543088E37F49EE4133F4456A0BDE68B29708572A45 | 601B288BB32D80A7BA389F543088E37F49EE4133F4456A0BDE68B29708572A45 | DC08039EC39C8C49CDE266B989B05218CC40C46264B8DF500D5D41A18F2E515D | 801070DC8C5FB71C7C0FC205A536E31984A3CF4820D30E08A06B11EA2B295445 | 0xE000001F | no |
| Phase34 / success | E2 | 406E91243A9A664CEAA43D32DB67AAF8FAB99C9DB0C7EBED6D0A5A941C9E2DBB | D1D7AFFBF5C705D01D41508F343AB5E7FFF522B3C8679C8508C1B448875D8893 | 406E91243A9A664CEAA43D32DB67AAF8FAB99C9DB0C7EBED6D0A5A941C9E2DBB | 1A84ACE2076C7621DD132AC7519E0E6314C36647E6976286DD3147B50FF3C83A | 0xC600001F | no |
| Phase34 / failfast | E2 | 83DEEF5B708523FE2CC4D2EACB87E2906E4CBF4C1E7154363B703B55351ABC52 | 36603FEE21810E2FF13DAC4FF1693CFFC5AE36DBA7FB5A27E8F41F4704BFBF68 | 83DEEF5B708523FE2CC4D2EACB87E2906E4CBF4C1E7154363B703B55351ABC52 | D10DABB6B7CE03D7502FB5D0DA482E62AF067EAF2F21EC186184FDDE56CC559F | 0xCA00001F | no |
| Phase34 / stale-owner | E2 | 5AE2926A11065A887A302742382B358177DB9B5F59AD07670E4A379E9AAD0064 | 527112F8CD3B9708809E1502FBB255B42E0C0A05430A279655C75EAB923F3EB1 | 5AE2926A11065A887A302742382B358177DB9B5F59AD07670E4A379E9AAD0064 | 1D8FDBA74A2F0FB485FB0AEE6F279A59AE996E71D6E81F443D2D5F3683CFCB92 | 0xD200001F | no |
| Phase34 / cross-scope | E2 | EF07C78EA5EFFCECE2899536C556F16C2A9222B7A97595EA447D95164D62AF81 | 67ECC8FB13020EE7C82C126CCB0E43B475FC0B05515D1BF9C7581ACDEB7BAAEA | EF07C78EA5EFFCECE2899536C556F16C2A9222B7A97595EA447D95164D62AF81 | 810DF6BC663A8CADFFBB2251557A5383CEFE0486DAFF38EFB40516B20EEA353A | 0xE200001F | no |
| Phase34 / malformed | M | AB9517E99B0A53399E7F4B2C0FCDC6C017F26BA1686C2E767897935EA1E3F482 | AB9517E99B0A53399E7F4B2C0FCDC6C017F26BA1686C2E767897935EA1E3F482 | AB9517E99B0A53399E7F4B2C0FCDC6C017F26BA1686C2E767897935EA1E3F482 | 5518ED4ACD2699CDD666201C4C2E708444FB7861D840B2B5F8149F58AAA0F748 | 0xCC00001F | yes |
| Phase35 / success | E1 | 857EED52486729FFC0DB57DFAFB133EB45230AC0AFF44CA980B8BB1372138A59 | 857EED52486729FFC0DB57DFAFB133EB45230AC0AFF44CA980B8BB1372138A59 | 41BE458D632D0F8389644286391DB1CEF7B1F49DE2C237463F6E580BB00866AC | 7B5EE18675E12D9892D223357326C4C3EE1C9A220EB7D7221C30BEDF7720B0D8 | 0xFE00001F | no |
| Phase35 / failfast | E1 | 2764F99E279858A31941479D9318FF1DC17DA4A4098A0668BF854AC4AFAF8370 | 2764F99E279858A31941479D9318FF1DC17DA4A4098A0668BF854AC4AFAF8370 | 2F38501A6C020BCA518FBEBE4C6923915CD2858FC8134C17EB5023102FBF369E | 5243F45D26980D23AD3AC058F5755B13531735ED5B2FFC1D517E15CE22A4F182 | 0xDE00001F | no |
| Phase35 / stale-owner | E1 | 16CB43CA20EC77DEA363307A7836786929FAF311DC4E81FC69963CAD29880214 | 16CB43CA20EC77DEA363307A7836786929FAF311DC4E81FC69963CAD29880214 | DE45740312A5A2E47C161F3BFB51809F7B2C781180A15CCA242FE568F16ED647 | AED6916ACCA2C4D4914A54B8B162394DEFDCD12F5A02871A0DAE03455A66EE19 | 0xEE00001F | no |
| Phase35 / cross-scope | E1 | 2671FAF3929F9B9B91CECAD0BBD8D22E84895D38DB766D0AFEB2D3890B296504 | 2671FAF3929F9B9B91CECAD0BBD8D22E84895D38DB766D0AFEB2D3890B296504 | D463F62A598827B91487A8B9797849A3842CE3123E6A86BD027EEC51CE49ED2A | 0A4536599512A756B5CCC550660CF0B7F121156C43CC2A99A866B9C5C43F38B4 | 0xF600001F | no |
| Phase35 / malformed | E1 | 2C5714C64BF8E4AA3D5CE945CA8C04ED4ECE80818D6D8024884452CA87518971 | 2C5714C64BF8E4AA3D5CE945CA8C04ED4ECE80818D6D8024884452CA87518971 | 181EF3A8DDEDC5736ED958A00BA1F685E806CE2BE7326C82E399B6313310D40B | 4C9084DEBE613BA6CC7EA2B4F582C081B975489917C0A63FA3F4D7E4514229F0 | 0xFA00001F | no |
| Phase35 / no-read | E1 | A9F1962118B00F8975AF2097187D94CBE0ADAF41B60254CD5838214EC56BB3B7 | A9F1962118B00F8975AF2097187D94CBE0ADAF41B60254CD5838214EC56BB3B7 | 4F96DF400BE2556764201C0A4E16FC42F1645D02CE03DAB70A4FFF2AA0F86773 | 6B1038516D0039C96716B23C8474B8726F7EC94AE2042F004C949DE9C951FCA8 | 0xFC00001F | no |
| Phase35 / one-read | E1 | 402DEAB478471F31DAD0CAE544322176BF71B426DB41DC8128B0EBE2898C06AA | 402DEAB478471F31DAD0CAE544322176BF71B426DB41DC8128B0EBE2898C06AA | CD2B0721054928520802D27C58B5AECAC5211D90CEC7BD15CAA868E7E8E34993 | 231887941B373728EFFD016F2134BB87E3FB283E151327727D8DCFCE65BEA952 | 0xF400001F | no |
| Phase35 / two-read | E1 | DEA6589984974B577039A124BE46E4037310BEDC61F4410653C8421271A3E67B | DEA6589984974B577039A124BE46E4037310BEDC61F4410653C8421271A3E67B | BA550938E214F6E08447D35D047D25C3D99E920B87DAD594AF2603571F370CE3 | BD6E1C31203665B7D77A065DA2401B3AB064A566E77A6BF5CEB4E2369EFA4D68 | 0xEC00001F | no |
| Phase35 / not-found | E1 | 1F3EBFE34484DB946B8C71E579AB815AFDF223BFAD4D57E5833A61A6DF4F702D | 1F3EBFE34484DB946B8C71E579AB815AFDF223BFAD4D57E5833A61A6DF4F702D | 9461A4CAD49BC2AD5690127F26CE8EDEDF15C59245D27B2D84FBE40388BA37DA | 8DC876E5F602AE819B14A229C0563D6D8E51F73923BEB5AE16D548C72D744DCB | 0xE400001F | no |

The counts are 36 E1 rows, four E2 rows, and one M row. Thus, the familiar “40/41 build/stage mismatches” contain two distinct hash patterns. The sole M row, Phase34 `malformed`, is internally consistent at the time of this audit but still lacks source and build-input provenance.

## 4. Record metadata and output paths for all variants

The build records store absolute executable paths. The following table preserves each record's mode, project basename, output path relative to its recorded output root, and recorded/output/staged byte lengths. `CACHE` and `ALT` name the output roots shown below the table. A `RECORD_OUTPUT_MATCH_STAGE_DIVERGED` row has a staged length/hash different from the current output; four Phase34 E2 rows have record/stage byte equality while the current output differs.

| Phase / variant | Mode | Proof project | Recorded output path | Bytes (record / output / stage) |
|---|---|---|---|---:|
| Phase29 / success | Success | guideXOS.UserManagedNotificationProof.csproj | CACHE/phase29-managed-notification\success\publish\guideXOS.Phase29ManagedNotificationProof.exe | 706048/706048/706048 |
| Phase29 / title-failure | TitleFailure | guideXOS.UserManagedNotificationProof.csproj | CACHE/phase29-managed-notification\title-failure\publish\guideXOS.Phase29ManagedNotificationProof.exe | 706048/706048/706048 |
| Phase29 / body-failure | BodyFailure | guideXOS.UserManagedNotificationProof.csproj | CACHE/phase29-managed-notification\body-failure\publish\guideXOS.Phase29ManagedNotificationProof.exe | 706560/706560/706560 |
| Phase29 / invalid-type | InvalidType | guideXOS.UserManagedNotificationProof.csproj | CACHE/phase29-managed-notification\invalid-type\publish\guideXOS.Phase29ManagedNotificationProof.exe | 706048/706048/706048 |
| Phase29 / failfast | FailFast | guideXOS.UserManagedNotificationProof.csproj | CACHE/phase29-managed-notification\failfast\publish\guideXOS.Phase29ManagedNotificationProof.exe | 706560/706560/706560 |
| Phase30 / success | Success | guideXOS.UserManagedClipboardProof.csproj | CACHE/phase30-managed-clipboard\success\publish\guideXOS.Phase30ManagedClipboardProof.exe | 708096/708096/708096 |
| Phase30 / reader | Reader | guideXOS.UserManagedClipboardProof.csproj | CACHE/phase30-managed-clipboard\reader\publish\guideXOS.Phase30ManagedClipboardProof.exe | 707584/707584/707584 |
| Phase30 / cross-writer | CrossWriter | guideXOS.UserManagedClipboardProof.csproj | CACHE/phase30-managed-clipboard\cross-writer\publish\guideXOS.Phase30ManagedClipboardProof.exe | 706048/706048/706048 |
| Phase30 / overwrite | Overwrite | guideXOS.UserManagedClipboardProof.csproj | CACHE/phase30-managed-clipboard\overwrite\publish\guideXOS.Phase30ManagedClipboardProof.exe | 708096/708096/708096 |
| Phase30 / empty | Empty | guideXOS.UserManagedClipboardProof.csproj | CACHE/phase30-managed-clipboard\empty\publish\guideXOS.Phase30ManagedClipboardProof.exe | 708096/708096/708096 |
| Phase30 / clear | Clear | guideXOS.UserManagedClipboardProof.csproj | CACHE/phase30-managed-clipboard\clear\publish\guideXOS.Phase30ManagedClipboardProof.exe | 708096/708096/708096 |
| Phase30 / oversize | Oversize | guideXOS.UserManagedClipboardProof.csproj | CACHE/phase30-managed-clipboard\oversize\publish\guideXOS.Phase30ManagedClipboardProof.exe | 708096/708096/708096 |
| Phase30 / malformed-length | MalformedLength | guideXOS.UserManagedClipboardProof.csproj | CACHE/phase30-managed-clipboard\malformed-length\publish\guideXOS.Phase30ManagedClipboardProof.exe | 708096/708096/708096 |
| Phase30 / failfast | FailFast | guideXOS.UserManagedClipboardProof.csproj | CACHE/phase30-managed-clipboard\failfast\publish\guideXOS.Phase30ManagedClipboardProof.exe | 706048/706048/706048 |
| Phase31 / success | Success | guideXOS.UserManagedShellProof.csproj | CACHE/phase31-managed-shell\success\publish\guideXOS.Phase31ManagedShellProof.exe | 706560/706560/706560 |
| Phase31 / invalid-target | InvalidTarget | guideXOS.UserManagedShellProof.csproj | CACHE/phase31-managed-shell\invalid-target\publish\guideXOS.Phase31ManagedShellProof.exe | 706560/706560/706560 |
| Phase31 / oversize | Oversize | guideXOS.UserManagedShellProof.csproj | CACHE/phase31-managed-shell\oversize\publish\guideXOS.Phase31ManagedShellProof.exe | 706560/706560/706560 |
| Phase31 / failfast | FailFast | guideXOS.UserManagedShellProof.csproj | CACHE/phase31-managed-shell\failfast\publish\guideXOS.Phase31ManagedShellProof.exe | 706560/706560/706560 |
| Phase31 / stale-owner | StaleOwner | guideXOS.UserManagedShellProof.csproj | CACHE/phase31-managed-shell\stale-owner\publish\guideXOS.Phase31ManagedShellProof.exe | 706560/706560/706560 |
| Phase32 / success | Success | guideXOS.UserManagedOpenDocumentProof.csproj | CACHE/phase32-managed-open-document\success\publish\guideXOS.Phase32ManagedOpenDocumentProof.exe | 706560/706560/706560 |
| Phase32 / failfast | FailFast | guideXOS.UserManagedOpenDocumentProof.csproj | CACHE/phase32-managed-open-document\failfast\publish\guideXOS.Phase32ManagedOpenDocumentProof.exe | 706560/706560/706560 |
| Phase32 / stale-owner | StaleOwner | guideXOS.UserManagedOpenDocumentProof.csproj | CACHE/phase32-managed-open-document\stale-owner\publish\guideXOS.Phase32ManagedOpenDocumentProof.exe | 706560/706560/706560 |
| Phase33 / success | Success | guideXOS.UserManagedShellActionProof.csproj | CACHE/phase33-managed-shell-action\success\publish\guideXOS.Phase33ManagedShellActionProof.exe | 706560/706560/706560 |
| Phase33 / failfast | FailFast | guideXOS.UserManagedShellActionProof.csproj | CACHE/phase33-managed-shell-action\failfast\publish\guideXOS.Phase33ManagedShellActionProof.exe | 706560/706560/706560 |
| Phase33 / stale-owner | StaleOwner | guideXOS.UserManagedShellActionProof.csproj | CACHE/phase33-managed-shell-action\stale-owner\publish\guideXOS.Phase33ManagedShellActionProof.exe | 706560/706560/706560 |
| Phase33 / invalid-action | InvalidAction | guideXOS.UserManagedShellActionProof.csproj | CACHE/phase33-managed-shell-action\invalid-action\publish\guideXOS.Phase33ManagedShellActionProof.exe | 706560/706560/706560 |
| Phase33 / malformed | Malformed | guideXOS.UserManagedShellActionProof.csproj | CACHE/phase33-managed-shell-action\malformed\publish\guideXOS.Phase33ManagedShellActionProof.exe | 706560/706560/706560 |
| Phase34 / success | Success | guideXOS.UserManagedResourceReadProof.csproj | CACHE/phase34-managed-resource-read\payloads\success\publish\guideXOS.Phase34ManagedResourceReadProof.exe | 709120/709120/709120 |
| Phase34 / failfast | FailFast | guideXOS.UserManagedResourceReadProof.csproj | CACHE/phase34-managed-resource-read\payloads\failfast\publish\guideXOS.Phase34ManagedResourceReadProof.exe | 708096/708096/708096 |
| Phase34 / stale-owner | StaleOwner | guideXOS.UserManagedResourceReadProof.csproj | CACHE/phase34-managed-resource-read\payloads\stale-owner\publish\guideXOS.Phase34ManagedResourceReadProof.exe | 707584/707584/707584 |
| Phase34 / cross-scope | CrossScope | guideXOS.UserManagedResourceReadProof.csproj | CACHE/phase34-managed-resource-read\payloads\cross-scope\publish\guideXOS.Phase34ManagedResourceReadProof.exe | 707584/707584/707584 |
| Phase34 / malformed | Malformed | guideXOS.UserManagedResourceReadProof.csproj | ALT1/phase34-managed-resource-read\payloads\malformed\publish\guideXOS.Phase34ManagedResourceReadProof.exe | 708096/708096/708096 |
| Phase35 / success | Success | guideXOS.UserManagedPersistentStorageProof.csproj | CACHE/phase35-managed-persistent-read\payloads\success\publish\guideXOS.Phase35ManagedPersistentReadProof.exe | 710144/710144/710656 |
| Phase35 / failfast | FailFast | guideXOS.UserManagedPersistentStorageProof.csproj | CACHE/phase35-managed-persistent-read\payloads\failfast\publish\guideXOS.Phase35ManagedPersistentReadProof.exe | 708096/708096/708096 |
| Phase35 / stale-owner | StaleOwner | guideXOS.UserManagedPersistentStorageProof.csproj | CACHE/phase35-managed-persistent-read\payloads\stale-owner\publish\guideXOS.Phase35ManagedPersistentReadProof.exe | 708096/708096/708096 |
| Phase35 / cross-scope | CrossScope | guideXOS.UserManagedPersistentStorageProof.csproj | CACHE/phase35-managed-persistent-read\payloads\cross-scope\publish\guideXOS.Phase35ManagedPersistentReadProof.exe | 708096/708096/708096 |
| Phase35 / malformed | Malformed | guideXOS.UserManagedPersistentStorageProof.csproj | CACHE/phase35-managed-persistent-read\payloads\malformed\publish\guideXOS.Phase35ManagedPersistentReadProof.exe | 707584/707584/708096 |
| Phase35 / no-read | NoRead | guideXOS.UserManagedPersistentStorageProof.csproj | CACHE/phase35-managed-persistent-read\payloads\no-read\publish\guideXOS.Phase35ManagedPersistentReadProof.exe | 705024/705024/705024 |
| Phase35 / one-read | OneRead | guideXOS.UserManagedPersistentStorageProof.csproj | CACHE/phase35-managed-persistent-read\payloads\one-read\publish\guideXOS.Phase35ManagedPersistentReadProof.exe | 708096/708096/708096 |
| Phase35 / two-read | TwoRead | guideXOS.UserManagedPersistentStorageProof.csproj | CACHE/phase35-managed-persistent-read\payloads\two-read\publish\guideXOS.Phase35ManagedPersistentReadProof.exe | 708096/708096/708096 |
| Phase35 / not-found | NotFound | guideXOS.UserManagedPersistentStorageProof.csproj | CACHE/phase35-managed-persistent-read\payloads\not-found\publish\guideXOS.Phase35ManagedPersistentReadProof.exe | 708096/708096/708096 |

Output roots: CACHE = C:\Users\guideX\AppData\Local\Packages\OpenAI.Codex_2p2nqsd0c76g0\LocalCache\Local\guideXOSUEFI-Phase35Q\dotnet; ALT1 = C:\Users\guideX\AppData\Local\guideXOSUEFI-Phase35Q\dotnet.

Each phase record names its corresponding `Tools/PhaseNN/build_phaseNN_*.ps1` producer. Every record's SDK project is the mainline `GuideXos.User/GuideXos.User.csproj`; the proof project varies by row as shown.

## 5. Bounded hash search and staging behavior

The auditor searched only the explicitly selected checkout roots, phase-specific output layouts (`<phase>/<mode>/publish/<recorded executable name>` and `<phase>/payloads/<mode>/publish/<recorded executable name>`), and the absolute paths already named by the records. It did not walk arbitrary drives or recursively search unrelated trees. It found a receiving-stage hash match only for the Phase34 `malformed` executable, at its recorded output path. No candidate output matched the staged hashes of the other 40 rows within those known locations.

All seven stage scripts load the phase build record, copy each recorded `$artifact` into `ramdisk_src/Native`, then generate that row's GXMI descriptor from the same `$artifact`. Phases 31–35 also run their phase-specific validation after the copy. A completed run of these scripts against the current records and current output paths would stage the current recorded outputs. It cannot explain the present E1 pattern. For E2, the stage and record agree but the file currently at the recorded path does not; a later output replacement is possible, but no log or authenticated history identifies such an operation.

The identical staged pair set in four Git checkouts is evidence that the same bytes were propagated or shared. The inspected Phase29 sample is tracked in mainline history at commit `b0a50b924da0e1d76d50b2b58de01418d4b41d74` (2026-10-17). Timestamps place most recorded outputs/records earlier than the staged files, but timestamps and one path's Git history do not identify the originating build or prove a packaging/refresh event. No packaging, staging, synchronization, or refresh record was found that binds the 40 mismatches to a later artifact set.

## 6. Supported lineage explanation

| Explanation | Finding |
|---|---|
| A — stale build records | Not established. The 36 E1 records agree with their current outputs, but neither source is bound to an immutable source/tool fingerprint. |
| B — stale staged payloads | Not established. The E1 stage may be from a different build, but no trusted provenance identifies it as stale. |
| C — packaging divergence | Not established. Shared staging bytes and dates are suggestive context, but no packaging or refresh evidence ties them to these rows. |
| D — identity/tooling mismatch | Rejected as the common explanation. The auditor compares raw executable SHA-256 and lengths; the GXMI digests agree with staged bytes, descriptor flags validate, and the normalized PE timestamp checks pass. The observed hashes are genuinely different byte sequences. |
| E — unresolved provenance | Supported for the 40 mismatched rows and for the authoritative origin of the complete 41-row set. |

The split E1/E2 patterns do not support one proven replacement operation. For E1, the current recorded output and record agree while the stage differs. For E2, the stage and record agree while the current output differs. Current evidence cannot distinguish which set is newer, intended, or authentic. The matched Phase34 `malformed` row is a current consistency result, not proof of the originating source build.

## 7. Safe recovery strategy

Do not restage the 36 E1 outputs, restore the four E2 outputs over the current stage, or rewrite records, hashes, GXMI descriptors, or identities to force agreement. None of those operations would restore missing source provenance. The identity source's agreement with current staging is not a substitute for the build-to-stage chain.

For a new trusted 41-row proof set, the smallest defensible action is a controlled Phase29–35 regeneration in the intended branch, with each build record created by the same run as its executable, the phase stage script consuming that exact record/output, and an immutable source/tool-input envelope captured before the set is accepted. The current set has only one internally matching row and no source fingerprint on any row, so selectively reusing the existing 40 mismatches cannot produce a trusted complete set. BUILD1B did not launch that rebuild: runtime/package prerequisites and the provenance envelope are not yet qualified, and the supported full build continues into hosted kernel compilation.

On completion of a later recovery run, validate all 41 raw EXE hashes, lengths, GXMI identities/flags, build records, generated identities, and phase-specific checks. Run the existing native generator audit before generating identities, generate identities only from the accepted staged set, then run the post-kernel and final ramdisk audits. Retain original records and build logs as evidence.

## 8. Runtime prerequisite findings

Mainline's runtime source is at the locked commit `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`. The recovery locks validate 59 source/template files; Phase21–23 patch evidence validates; the private `ilc.exe` hash is `268E0499447281B75ACBB6935DA5E710190D84285D16E38AF96E610786F1775D`. APP_MODEL's runtime lock content is semantically compatible, but APP_MODEL has no local runtime source, patch records, private ILC, runtime pack, or local managed build records. A mainline cache audit against APP_MODEL locks can validate content compatibility, but it reports that this content is not materializable under APP_MODEL's current branch-local build contract.

The mainline runtime-pack manifest has valid target/package identity and hashes its runtime/compiler assets and link shim, but it omits hashes and lengths for the three `.nupkg` files. The package archives' internal `.nuspec` files identify the package IDs and version `9.0.0`; package name/version alone does not bind their bytes. BUILD1B computed the following SHA-256 values for the actual archives. These are measurement values for the inspected files, not an independent authenticity endorsement:

| Nuspec ID / version | Archive | Bytes | SHA-256 (measured) |
|---|---|---:|---|
| Microsoft.DotNet.ILCompiler 9.0.0 | microsoft.dotnet.ilcompiler.9.0.0.nupkg | 42,562,402 | E4E0BB1D6057A0BFD9B2F5357001A05B9320E740BF054F377D5426F363E2C41A |
| runtime.guidexos-x64.Microsoft.DotNet.ILCompiler 9.0.0 | runtime.guidexos-x64.microsoft.dotnet.ilcompiler.9.0.0.nupkg | 5,853,529 | FDAEA2A4222985FB06A0D42DFFD5838DCF0726FDEA17C0139585925331A250E8 |
| runtime.win-x64.Microsoft.DotNet.ILCompiler 9.0.0 | runtime.win-x64.microsoft.dotnet.ilcompiler.9.0.0.nupkg | 81,801,976 | 0E3E1D91A1994607F6DCED80E961E921D75E015C1F9A77EAD6A7A597EE8DDD2A |

The corresponding NuGet global-cache entries have `.nupkg.sha512` sidecars, but their archive lengths differ from the generated runtime-pack archives (94,981 vs. 42,562,402 bytes; 50,779,716 vs. 5,853,529 bytes; and 50,803,315 vs. 81,801,976 bytes). Those sidecars therefore do not attest the pack files. For future intake, verify each `.nuspec` ID/version, verify the exact archive bytes against a trusted restore/feed content hash or approved package source, and store the archive's SHA-256 and length in the runtime manifest. A local hash detects later corruption; it is not by itself an authenticity root.

The pinned runtime source, lock files, patch identities/file hashes, and private ILC can be reused by verified copy into an isolated branch followed by the existing lock/evidence checks. The Phase23 runtime pack and its records contain absolute checkout paths. The current build scripts expect branch-local `out/rt` and `out/dotnet/phase23-runtime-pack`; they patch/build under the checkout. A shared read-only content store is feasible as an immutable source of copy-and-recheck inputs, not as a direct shared `out/rt` path or writable junction. Runtime-pack byte-for-byte determinism is not established because the package bytes are unhashed in the manifest and the producing toolchain/environment is not fingerprinted.

## 9. APP_MODEL hosted compilation prerequisites

APP_MODEL is not currently buildable through its hosted C# compilation path. Its staged pairs happen to equal the other three checkouts, but it lacks the records and branch-local dependencies that the normal build consumes, and its build-contract fingerprint differs from mainline. The current comparison found 94 differing contract inputs; BUILD1A had already identified that mainline artifacts cannot be treated as compatible APP_MODEL inputs.

1. **Restore managed runtime prerequisites.** Populate APP_MODEL's branch-local `out/rt` at the pinned runtime commit; verify the full 59-file/template lock and Phase21–23 evidence; restore the private ILC and runtime pack; bind and verify all package archive hashes. Reuse mainline runtime bytes only through isolated copy plus APP_MODEL lock/hash rechecks.
2. **Create Phase29–35 evidence.** Build all seven phase cohorts from APP_MODEL's own source contract so the seven build records and all 41 output variants bind to this branch's source/tool inputs. Mainline's existing records do not exist in APP_MODEL and the current comparison does not establish compatibility for reusing mainline proof outputs.
3. **Stage APP_MODEL locally.** Run each phase's stage script so `ramdisk_src/Native` receives the 41 EXEs and descriptors generated from the same APP_MODEL records. Audit every executable hash/length and GXMI flag/digest.
4. **Generate identities.** After the 41-row native audit passes, the supported generator invocation is `python -B Tools/ManagedArtifacts/managed_artifacts.py --stage generated --identity-source guideXOS/obj/ManagedArtifactIdentities.g.cs`. The full build performs this step as part of its pipeline; this explicit invocation describes the identity prerequisite for a staged/manual qualification.
5. **Qualify hosted compilation.** Run `.\build.ps1 -UefiDiagnosticMode AppModel` in APP_MODEL, confirm the Release kernel publish succeeds, then confirm the post-kernel identity audit and final ramdisk audit. BUILD1B did not run this command and makes no buildability claim.

## 10. Auditor changes and tests

`Tools/ManagedArtifacts/uefi_build_bootstrap.py` remains read-only. BUILD1B adds per-row record/output/stage classifications, recorded metadata and source/input fingerprint completeness, staged pair-set signatures across explicit roots, bounded candidate-output hash search, and recovery prerequisite summaries. Fingerprint presence requires a 40-hex immutable source commit and a 64-hex input/tool hash; arbitrary labels are not counted as provenance. No automatic repair or artifact installation was added, and the existing managed identity generator was not changed.

Focused regression coverage now includes both opposite record/output/stage mismatch patterns, finding a matching output in a bounded phase directory, identifying identical complete staged pair sets without mutation, and rejecting non-hash fingerprint labels. The existing BUILD1A synthetic checks remain in the suite. Synthetic executables stay inside temporary fixture directories.

Command: `python -B -m unittest discover -s Tests/ManagedArtifacts -p 'test_uefi_build_bootstrap.py' -v`

Result: **17 tests passed**.

Genuine artifacts were checked by the BUILD1B auditor and the native managed-artifact generator in read-only `--audit-only` mode. The native check refused mainline at Phase29 `success` due to exact build/stage byte inequality. No kernel publish, full Phase29–35 rebuild, QEMU, QAC admission, or guest test was run.

## 11. Recommended BUILD1C scope

1. Define a versioned provenance envelope binding repository and immutable source commit, clean/dirty state, relevant source/input tree fingerprint, phase builder and descriptor/stage script hashes, SDK/compiler/ILC/toolchain identities, runtime-pack manifest digest, output filename/length/SHA-256, and the generated record digest. Preserve original records and add an approved trust/signing mechanism before treating the envelope as authoritative.
2. Add package archive SHA-256/length and verified NuGet content hashes to the runtime manifest. Validate package ID/version from each archive's `.nuspec` and require byte equality with the trusted archive hash.
3. Qualify copy-based runtime materialization in an isolated checkout. Keep writable `out/rt`, phase outputs, generated identities, kernel outputs, and ramdisk products local to each checkout.
4. Regenerate all 41 managed variants under a captured provenance envelope, run the native identity audit, and complete a hosted Release compile plus post-kernel/final-ramdisk audits. Keep QEMU and QAC out of this infrastructure qualification.

BUILD1C should not add a convenience path that accepts current staged bytes without source provenance or relaxes the identity generator's checks.
