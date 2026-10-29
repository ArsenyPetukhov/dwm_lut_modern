## verdict

1. **Resolved — contrast.** Actual Settings captures show the underlined help link at #36363B on #FFFFFF in light mode (**12.02:1**) and #DEDEE3 on #2F2F32 in dark mode (**9.95:1**). Actual library captures show empty-state text at #646469 on #FAFAFB (**5.64:1**) and #B8B8BF on #242426 (**7.86:1**). The dominant antialiased stroke pixels also remain above 4.5:1: 5.56:1 light and 7.60:1 dark. Both previously failing surfaces now satisfy the requested normal-text contrast floor.
2. **Resolved — navigation copy.** Both English library captures visibly say “Displays”; the obsolete “Home page” destination is gone.

Evidence: all 34 expected recaptures exist with sensible dimensions; the four scoring frames were opened at full size and the complete capture matrix was checked for malformed or mismatched evidence. No fix-batch regression was observed. This pass scores the two findings only; no detector or GPU-playback claim is involved.

## remaining

Clear. This ship covers the scored fixes, not the whole surface.

disposition: ship
