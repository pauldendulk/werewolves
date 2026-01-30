# Development Instructions for GitHub Copilot

## Critical: Build and Test After Every Change

**Before stopping work on any task, you MUST:**

1. **Build the project** - Every change requires a fresh build
2. **Inspect build errors** - Carefully review all compilation errors and warnings
3. **Fix all errors** - Do not proceed until the build is clean
4. **Run tests** - Execute the test suite to verify your changes
5. **Ensure tests pass** - All tests must be green before you stop working

## Building the Project

### Backend (.NET)
```powershell
cd backend/WerewolvesAPI
dotnet build
```

Check for compilation errors and warnings. Fix any issues before proceeding.

### Frontend (Angular)
```powershell
cd frontend/werewolves-app
npm run build
```

Review the build output for errors. The Angular compiler will show detailed error messages if anything is wrong.

## Running Tests

### Backend Tests
```powershell
cd backend/WerewolvesAPI.Tests
dotnet test
```

All tests must pass. If tests fail:
- Review the test output
- Fix the failing tests or your code
- Re-run until all tests are green

### Frontend Tests
```powershell
cd frontend/werewolves-app
npm test
```

Ensure all tests pass before completing the task.

## Adding Tests

**Every feature or bug fix should include tests:**

- **Backend**: Add tests in `backend/WerewolvesAPI.Tests`
  - Use FluentAssertions for readable assertions
  - Follow existing test patterns
  - Test both success and error cases

- **Frontend**: Add tests in component `.spec.ts` files
  - Test component logic
  - Test user interactions
  - Mock dependencies appropriately

## Development Workflow

1. **Make your change** - Edit code, add features, fix bugs
2. **Build immediately** - `dotnet build` or `npm run build`
3. **Check build output** - Read all errors and warnings carefully
4. **Fix build errors** - Resolve issues before moving forward
5. **Write/update tests** - Add tests for your changes
6. **Run all tests** - Ensure nothing broke
7. **Verify tests pass** - All tests must be green
8. **Repeat** - If tests fail, fix and rebuild

## Common Build Issues

### Frontend
- **Import errors**: Check file paths and module imports
- **Type errors**: Ensure TypeScript types are correct
- **Dependency issues**: Run `npm install` if packages are missing

### Backend
- **Namespace errors**: Verify using statements
- **Type mismatches**: Check method signatures and return types
- **NuGet issues**: Run `dotnet restore` if packages are missing

## Starting the Application

Use the provided scripts to start both frontend and backend:

```powershell
# From project root
.\start.ps1
```

This will:
- Stop any existing processes on ports 5000 and 4200
- Start the .NET backend on http://localhost:5000
- Start the Angular frontend on http://localhost:4200

## Port Information

- **Frontend**: http://localhost:4200
- **Backend**: http://localhost:5000/api
- **Swagger**: http://localhost:5000/swagger

## Task Completion Checklist

Before marking a task as complete:

- [ ] Code builds without errors (`dotnet build` and `npm run build`)
- [ ] All existing tests pass
- [ ] New tests added for your changes
- [ ] All new tests pass
- [ ] No console errors when running the application
- [ ] Changes have been verified in the running application

---

**Remember: Never stop work without ensuring the build is clean and all tests pass!**
