# Test Suite Structure

Use one fast xUnit test project for the baseline. Split by behavior in file names:

- domain tests
- application/service tests
- repository/persistence tests
- view-model and navigation tests
- release/script/docs contract tests

Release packaging and screenshot acceptance should stay script-driven so the normal test loop remains fast.

