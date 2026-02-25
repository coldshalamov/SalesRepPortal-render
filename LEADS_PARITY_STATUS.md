# Leads Parity Status (TheRxSpot -> SalesRepPortal-render)

This tracks how close the `/Leads` tab is to the TheRxSpot sales pipeline while keeping porting to the real production repo safe.

## What is already matched

- Pipeline board between lead stages (drag/drop and stage buttons)
- Role-aware stage permissions (convert action stays restricted)
- Follow-up tasks:
  - add
  - complete
  - delete selected
  - quick task templates
- Overdue follow-up count in top stats
- Lead detail modal from board
- Notes preview in board cards + full notes in detail modal
- Board/table toggle with remembered preference per browser

## Still not 1:1 parity (intentional for safety right now)

- TheRxSpot inline **Add/Edit/Delete lead inside the same modal flow** is not fully mirrored.
  - Current app keeps safer existing flows (`Create`, `Edit`, `Details`) and links out from the modal.
- TheRxSpot **pipeline value** metric is not implemented.
  - Current lead schema has no value field yet.
- TheRxSpot has some API-style convenience actions (`assign`, `auto_assign`) that do not map directly to this app's existing reassignment flows.

## Safe next steps for full parity

1. Add optional lead value/source fields as **additive schema** only.
2. Add inline modal create/edit only after UX approval (to avoid duplicate workflows).
3. Keep destructive operations out of first production parity PR.
4. Generate and test SQL Server migration in the real repo before merge.

## Portability classification

- Current parity tranche: **Portable with edits**
- Required edit in real repo before production use:
  - generate and validate EF migration for `LeadFollowUpTask` on SQL Server
