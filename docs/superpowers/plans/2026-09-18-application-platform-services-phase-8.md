# guideXOS Application Platform Services — Phase 8 Implementation Plan

> For agentic workers: REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Build and runtime-prove a bounded C# application-platform-service boundary for notifications, application-scoped session settings, and immutable system-information snapshots while preserving the Phase 7 App Model authority and regression gates.

**Architecture:** Add a fixed ApplicationServiceRegistry with typed access backed by three internal C# adapters. Every operation receives or derives an ApplicationServiceContext, revalidates its generation-safe ApplicationInstanceHandle, checks common terminal/closing/suspended rejection, and then applies service-specific lifecycle eligibility. Settings are stored in a fixed application-identity table keyed by descriptor ID, while system information is returned as a copied immutable scalar snapshot.

**Tech Stack:** C# NativeAOT/UEFI runtime, existing managed App Model and WindowManager backends, fixed arrays/bounded strings, embedded deterministic self-tests, QEMU serial diagnostics, PowerShell build/validation scripts.

**Spec:** Docs/superpowers/specs/2026-09-17-application-platform-services-phase-8-design.md

## Global Constraints

- Do not modify D:\dev\guideXOSServer or D:\dev\guideXOS.
- ApplicationInstanceRegistry remains the only instance authority.
- ApplicationServiceContext is a validated capability-shaped value, not an authority.
- Use a bounded fixed service table; do not add a generic dependency-injection framework or an unbounded hot-path dictionary.
- Every service call revalidates the generation-safe handle and descriptor identity.
- Reject stale, terminated, failed, closing, and suspended contexts.
- Service backends may impose narrower lifecycle eligibility than the common validator.
- Session settings are keyed by stable descriptor/application ID, not instance handle.
- Instance-specific transient state remains in ApplicationInstance or the application implementation.
- Session settings are non-persistent in this phase.
- Concrete bounds are mandatory for notification text, capability metadata, application IDs, settings applications/keys/values, and system-information strings.
- System information is a copied immutable snapshot containing scalar values only.
- No raw Desktop, WindowManager, framebuffer, allocator, timer, thread-pool, or kernel-global object is exposed through the service contract.
- PermissionDenied remains available but no capability enforcement is invented without authoritative Server evidence.
- Preserve the Phase 7 baseline: fallback count 0, legacy backend count 0, stale ownership 0, allocator corruption 0, ThreadPool.Locked=0, valid graphics, and zero unexpected input drops.
- Keep dialogs, app-local storage, resources, clipboard, and shell/open services inventoried and documented but outside the selected implementation cohort.

---

## File map

- Create guideXOS/OS/ApplicationServices.cs — service IDs, bounded context, result types, requests, setting values, immutable system snapshot, and typed service interfaces.
- Create guideXOS/OS/ApplicationServiceRegistry.cs — fixed registration table, context creation/validation, typed access, lifecycle policy, application-keyed settings namespace coordination, counters, and deterministic self-test.
- Create guideXOS/OS/ApplicationServiceBackends.cs — C# notification, session-settings, and system-information backend adapters.
- Modify guideXOS/GUI/NotificationManager.cs — source application identity on service-created notifications and application-scoped clearing while preserving existing direct callers.
- Modify guideXOS/OS/ApplicationInstance.cs — service reset hooks and the bounded runtime service diagnostic.
- Modify guideXOS/OS/ApplicationFactories.cs — create service context/access at the existing factory boundary and pass them to migrated application objects.
- Modify guideXOS/DefaultApps/Calculator.cs — route calculator notifications through typed service access.
- Modify guideXOS/GUI/DisplayOptions.cs — route display-option notifications through typed service access.
- Modify guideXOS/DefaultApps/Notepad.cs — route wrap preference reads/writes through application-scoped session settings.
- Modify guideXOS/DefaultApps/TaskManager.cs — consume immutable system-information snapshots for the migrated metrics path.
- Modify guideXOS/Program.cs — initialize the service registry, invoke its deterministic self-test, and emit AppModel/AppRuntime service markers.
- Modify run_uefi_validation.ps1 — parse and gate the new service markers without weakening existing Phase 7 gates.
- Modify APP_MODEL_CONVERGENCE.md — add the authoritative Server↔C# inventory, convergence matrix, Phase 8 contract, migrated applications, deferred services, and acceptance evidence.

## Concrete bounds

| Object | Bound |
|---|---:|
| Application ID / descriptor ID | 96 characters |
| Capability count per context | 8 |
| Capability text | 64 characters |
| Service registry registrations | 3 selected services, fixed table capacity 8 |
| Notification title | 64 characters |
| Notification body | 256 characters |
| Settings application namespaces | 32 |
| Settings keys per application | 16 |
| Settings key | 64 characters |
| Settings string value | 256 characters |
| System-information OS name | 32 characters |
| System-information OS version | 32 characters |
| System-information architecture | 16 characters |
| Service-result diagnostic | 192 characters |

## Task 1: Establish preflight evidence and authoritative service inventory

Files:
- Read APP_MODEL_CONVERGENCE.md.
- Read Server app manifests/registry, notification/dialog/shell/VFS/system sources.
- Read C# App Model, notification/settings/dialog/system-information callers.
- Modify APP_MODEL_CONVERGENCE.md.

Interfaces:
- Consumes the Phase 7 authority/lifecycle/taskbar contract and both reference trees.
- Produces a source-located service inventory and convergence matrix.

- [ ] Step 1: Record starting repository state.

    git status --short --branch
    git rev-parse HEAD
    git log -1 --pretty=%s
    git rev-parse --abbrev-ref --symbolic-full-name '@{upstream}'
    git rev-list --left-right --count '@{upstream}...HEAD'
    git status --short --untracked-files=all

- [ ] Step 2: Inventory Server services by contract, not filename.

    rg -n -i 'NotificationManager|MessageBox|OpenDialog|SaveDialog|permissions|capabilit|settings|clipboard|file_clipboard|VFS|uptime|clock|memory|thread|CPU|LaunchTarget|AppLaunchResolver|shell' D:\dev\guideXOSServer --glob '*.h' --glob '*.cpp' --glob '*.md' --glob '!build/**' --glob '!out/**'

Record public call shape, context/permission behavior, synchronous/asynchronous behavior, error model, ownership, shell involvement, lifecycle relationship, and backend.

- [ ] Step 3: Inventory C# direct access and classify every meaningful usage.

    rg -n -i 'NotificationManager|Configuration|UISettings|SystemMode|Timer\.Ticks|Allocator\.|ThreadPool\.|OpenDialog|SaveDialog|MessageBox|FS\.File|Desktop\.|WindowManager\.' guideXOS -g '*.cs'

- [ ] Step 4: Add the new Application platform services section and a matrix with source locations, classification, semantics, backend difference, migration value, risk, and decision.

    | Service | Server source | C# source | Classification | Common semantics | Backend difference | Migration value | Risk | Decision |
    |---|---|---|---|---|---|---|---|---|
    | Notifications | notification_manager.h/.cpp | guideXOS/GUI/NotificationManager.cs | SAME IDEA / DIFFERENT API | bounded source notification | compositor toast versus C# toast | high | low | cohort |

Add rows for session settings, system information, dialogs, app-local storage, resources, clipboard, and shell/open services using the evidence actually found.

- [ ] Step 5: Check and commit the audit slice.

    git diff --check
    git add APP_MODEL_CONVERGENCE.md
    git commit -m "docs: audit Phase 8 application services"

## Task 2: Define bounded service contracts and immutable values (TDD)

Files:
- Create guideXOS/OS/ApplicationServices.cs.
- Consume the self-test scaffold in guideXOS/OS/ApplicationServiceRegistry.cs.

Interfaces:
- Produces ApplicationServiceId, ApplicationServiceResultCode, ApplicationServiceContext, ApplicationServiceResult, ApplicationServiceResult<T>, ApplicationNotificationRequest.Create/TryCreate, ApplicationSettingValue.Boolean/Int32/String, SystemInformationSnapshot.IsWithinBounds, and typed service interfaces.

- [ ] Step 1: Add failing assertions for bounded requests and immutable snapshot shape.

    Check(SystemInformationSnapshot.IsBoundedText("guideXOS", 32),
        "system snapshot text bound", ref passed, ref failed, ref failure);
    Check(!ApplicationNotificationRequest.TryCreate(
            "title",
            new string('x', ApplicationNotificationRequest.MaxBodyLength + 1),
            ApplicationNotificationSeverity.Info, out request),
        "notification body bound", ref passed, ref failed, ref failure);

- [ ] Step 2: Run AppModel and verify the expected missing-contract failure.

    powershell -NoProfile -ExecutionPolicy Bypass -File .\run_uefi_validation.ps1 -AppModel -TimeoutSeconds 120 -SerialLog serial_phase8_contract_red.txt

- [ ] Step 3: Implement the minimal bounded contracts and these exact typed operations.

    public interface IApplicationNotificationService {
        ApplicationServiceResult Publish(
            ApplicationServiceContext context,
            ApplicationNotificationRequest request);
        ApplicationServiceResult Clear(ApplicationServiceContext context);
    }

    public interface IApplicationSettingsService {
        ApplicationServiceResult<ApplicationSettingValue> Get(
            ApplicationServiceContext context, string key);
        ApplicationServiceResult Set(
            ApplicationServiceContext context, string key,
            ApplicationSettingValue value);
        ApplicationServiceResult Remove(
            ApplicationServiceContext context, string key);
    }

    public interface IApplicationSystemInformationService {
        ApplicationServiceResult<SystemInformationSnapshot> GetSnapshot(
            ApplicationServiceContext context);
    }

SystemInformationSnapshot contains copied uptime, memory total/used, thread count, CPU percentage, OS name/version, and architecture fields.

- [ ] Step 4: Run AppModel and verify contract assertions pass while registry lookup assertions remain red.

    powershell -NoProfile -ExecutionPolicy Bypass -File .\run_uefi_validation.ps1 -AppModel -TimeoutSeconds 120 -SerialLog serial_phase8_contract_green.txt

- [ ] Step 5: Commit the contract slice.

    git add guideXOS/OS/ApplicationServices.cs guideXOS/OS/ApplicationServiceRegistry.cs
    git commit -m "feat: define bounded application service contracts"

## Task 3: Implement fixed registry, context creation, typed access, and lifecycle validation (TDD)

Files:
- Modify guideXOS/OS/ApplicationServiceRegistry.cs.
- Modify guideXOS/OS/ApplicationServices.cs.
- Modify guideXOS/OS/ApplicationInstance.cs only for authoritative service reset hooks.

Interfaces:
- Produces ApplicationServiceRegistry.Initialize(), ResetForDiagnostics(), TryCreateContext(handle, out context, out result), TryGetAccess(context, out access, out result), TryCreateContextAndAccess(handle, out context, out access, out result), TryValidateContext(context, serviceId, out instance, out result), RunSelfTest(), and the internal deterministic self-test helpers used by the embedded assertions.

- [ ] Step 1: Add failing cases for duplicate registration, missing typed lookup, valid context, stale handle, and rejected lifecycle states.

    Check(ApplicationServiceRegistry.TryRegisterForSelfTest(
            ApplicationServiceId.Notifications),
        "duplicate service registration rejected", ref passed, ref failed, ref failure);
    Check(!ApplicationServiceRegistry.TryGetAccessForSelfTest(
            (ApplicationServiceId)99),
        "missing service rejected", ref passed, ref failed, ref failure);
    Check(ApplicationServiceRegistry.TryCreateContext(
            instance.Handle, out context, out result) && result.Succeeded,
        "valid service context", ref passed, ref failed, ref failure);

- [ ] Step 2: Run AppModel and confirm the registry cases fail for the expected missing implementation.

    powershell -NoProfile -ExecutionPolicy Bypass -File .\run_uefi_validation.ps1 -AppModel -TimeoutSeconds 120 -SerialLog serial_phase8_registry_red.txt

- [ ] Step 3: Implement a fixed registration table and validator. Register the three selected IDs in deterministic order. The common validator rejects terminal, closing, and suspended states. Service-specific masks are explicit: system information allows Loading, Initialized, Running, Activated, and Inactive; notifications and settings allow Initialized, Running, Activated, and Inactive.

    public static bool TryValidateContext(
            ApplicationServiceContext context,
            ApplicationServiceId serviceId,
            out ApplicationInstance instance,
            out ApplicationServiceResult result) {
        instance = null;
        result = ApplicationServiceResult.InvalidContextResult();
        if (context == null) return false;
        if (!ApplicationInstanceRegistry.TryGet(
                context.InstanceHandle, out instance)) return false;
        if (!TextEquals(instance.DescriptorId, context.ApplicationId))
            return false;
        if (ApplicationInstanceLifecycle.IsTerminalOrClosingOrSuspended(
                instance.LifecycleState)) return false;
        if (!IsServiceStateAllowed(serviceId, instance.LifecycleState))
            return false;
        result = ApplicationServiceResult.SuccessResult();
        return true;
    }

- [ ] Step 4: Run AppModel and verify registry self-test pass plus clean diagnostic instances/windows.

    powershell -NoProfile -ExecutionPolicy Bypass -File .\run_uefi_validation.ps1 -AppModel -TimeoutSeconds 120 -SerialLog serial_phase8_registry_green.txt

- [ ] Step 5: Commit the registry/context slice.

    git add guideXOS/OS/ApplicationServices.cs guideXOS/OS/ApplicationServiceRegistry.cs guideXOS/OS/ApplicationInstance.cs guideXOS/OS/ModernAppModel.cs
    git commit -m "feat: add validated application service registry"

## Task 4: Add notification backend and source-scoped presentation (TDD)

Files:
- Modify guideXOS/OS/ApplicationServiceBackends.cs.
- Modify guideXOS/GUI/NotificationManager.cs.
- Modify guideXOS/OS/ApplicationServiceRegistry.cs.

Interfaces:
- Produces CSharpApplicationNotificationService, source-tagged Notify objects, NotificationManager.AddForApplication, and NotificationManager.ClearForApplication.

- [ ] Step 1: Add failing publish, body-bound, invalid-context, and application-scoped-clear assertions.

    ApplicationNotificationRequest request =
        ApplicationNotificationRequest.Create(
            "Calculator", "Calculation complete",
            ApplicationNotificationSeverity.Info);
    ApplicationServiceResult published =
        access.Notifications.Publish(context, request);
    Check(published.Succeeded, "notification publish",
        ref passed, ref failed, ref failure);
    Check(access.Notifications.Publish(staleContext, request).Code ==
            ApplicationServiceResultCode.InvalidContext,
        "stale notification context", ref passed, ref failed, ref failure);

- [ ] Step 2: Run AppModel and confirm notification assertions fail before the backend exists.

    powershell -NoProfile -ExecutionPolicy Bypass -File .\run_uefi_validation.ps1 -AppModel -TimeoutSeconds 120 -SerialLog serial_phase8_notifications_red.txt

- [ ] Step 3: Translate the request into the existing toast manager without exposing Notify, Animation, or renderer fields through the contract. Tag service-created toasts with context application ID and clear only that source.

    public ApplicationServiceResult Publish(
            ApplicationServiceContext context,
            ApplicationNotificationRequest request) {
        ApplicationInstance instance;
        ApplicationServiceResult valid;
        if (!ApplicationServiceRegistry.TryValidateContext(
                context, ApplicationServiceId.Notifications,
                out instance, out valid)) return valid;
        if (request == null || !request.IsValid)
            return ApplicationServiceResult.InvalidRequestResult();
        NotificationManager.AddForApplication(
            context.ApplicationId,
            new Notify(request.RenderedMessage,
                request.Severity == ApplicationNotificationSeverity.Error
                    ? NotificationLevel.Error : NotificationLevel.None));
        return ApplicationServiceResult.SuccessResult();
    }

- [ ] Step 4: Run AppModel and verify notification self-tests pass.

    powershell -NoProfile -ExecutionPolicy Bypass -File .\run_uefi_validation.ps1 -AppModel -TimeoutSeconds 120 -SerialLog serial_phase8_notifications_green.txt

- [ ] Step 5: Commit the notification backend.

    git add guideXOS/OS/ApplicationServiceBackends.cs guideXOS/OS/ApplicationServiceRegistry.cs guideXOS/GUI/NotificationManager.cs
    git commit -m "feat: add application notification service"

## Task 5: Add application-scoped session settings backend (TDD)

Files:
- Modify guideXOS/OS/ApplicationServiceBackends.cs.
- Modify guideXOS/OS/ApplicationServiceRegistry.cs.

Interfaces:
- Produces CSharpApplicationSettingsService with Get, Set, and Remove; state is keyed by descriptor ID and shared by all valid instances of that descriptor.

- [ ] Step 1: Add failing tests for same-application sharing, cross-application isolation, missing-key behavior, invalid bounds, and stale/terminated rejection.

    ApplicationServiceResult set = access.Settings.Set(
        notepadContext1, "wrap",
        ApplicationSettingValue.Boolean(true));
    ApplicationServiceResult<ApplicationSettingValue> read =
        access.Settings.Get(notepadContext2, "wrap");
    Check(set.Succeeded && read.Succeeded && read.Value.BooleanValue,
        "same application shares session setting",
        ref passed, ref failed, ref failure);
    Check(access.Settings.Get(calculatorContext, "wrap").Code ==
            ApplicationServiceResultCode.NotFound,
        "different application setting namespace",
        ref passed, ref failed, ref failure);

- [ ] Step 2: Run AppModel and confirm settings assertions fail before the store exists.

    powershell -NoProfile -ExecutionPolicy Bypass -File .\run_uefi_validation.ps1 -AppModel -TimeoutSeconds 120 -SerialLog serial_phase8_settings_red.txt

- [ ] Step 3: Implement a fixed application-keyed store with 32 application slots and 16 key slots per application. Store only boolean, 32-bit integer, and bounded string values. Clear the store on ResetForDiagnostics; never write Configuration, SystemMode, or a filesystem path.

    private sealed class ApplicationSettingsNamespace {
        internal string ApplicationId;
        internal readonly SettingEntry[] Entries =
            new SettingEntry[MaxKeysPerApplication];
        internal int Count;
    }

- [ ] Step 4: Run AppModel and verify sharing, isolation, bounds, lifecycle rejection, and cleanup.

    powershell -NoProfile -ExecutionPolicy Bypass -File .\run_uefi_validation.ps1 -AppModel -TimeoutSeconds 120 -SerialLog serial_phase8_settings_green.txt

- [ ] Step 5: Commit the settings backend.

    git add guideXOS/OS/ApplicationServiceBackends.cs guideXOS/OS/ApplicationServiceRegistry.cs
    git commit -m "feat: add application-scoped session settings"

## Task 6: Add copied immutable system-information backend (TDD)

Files:
- Modify guideXOS/OS/ApplicationServiceBackends.cs.
- Modify guideXOS/OS/ApplicationServiceRegistry.cs.

Interfaces:
- Produces CSharpApplicationSystemInformationService.GetSnapshot returning ApplicationServiceResult<SystemInformationSnapshot> with copied scalar values and bounded strings.

- [ ] Step 1: Add failing tests for snapshot success, immutable value copying, text bounds, and backend failure.

    ApplicationServiceResult<SystemInformationSnapshot> snapshot =
        access.SystemInformation.GetSnapshot(context);
    Check(snapshot.Succeeded &&
            snapshot.Value.MemorySizeBytes >=
                snapshot.Value.MemoryInUseBytes,
        "system snapshot scalar values",
        ref passed, ref failed, ref failure);
    Check(snapshot.Value.IsWithinBounds,
        "system snapshot text bounds",
        ref passed, ref failed, ref failure);

- [ ] Step 2: Run AppModel and confirm snapshot assertions fail before the backend exists.

    powershell -NoProfile -ExecutionPolicy Bypass -File .\run_uefi_validation.ps1 -AppModel -TimeoutSeconds 120 -SerialLog serial_phase8_system_red.txt

- [ ] Step 3: Implement one request-time snapshot copy. Clamp CPU percentage to 0..100, preserve used <= total when totals are available, copy bounded OS/version/architecture strings, and return ResourceUnavailable when a required scalar cannot be obtained. Never return allocator, timer, or thread-pool objects.

    SystemInformationSnapshot snapshot =
        new SystemInformationSnapshot(
            Timer.Ticks,
            Allocator.MemorySize,
            Allocator.MemoryInUse,
            ThreadPool.ThreadCount,
            ClampCpu(ThreadPool.CPUUsage),
            "guideXOS", "Phase8", "x86_64");
    return ApplicationServiceResult<SystemInformationSnapshot>
        .SuccessResult(snapshot);

- [ ] Step 4: Run AppModel and verify snapshot tests pass.

    powershell -NoProfile -ExecutionPolicy Bypass -File .\run_uefi_validation.ps1 -AppModel -TimeoutSeconds 120 -SerialLog serial_phase8_system_green.txt

- [ ] Step 5: Commit the system-information backend.

    git add guideXOS/OS/ApplicationServiceBackends.cs guideXOS/OS/ApplicationServiceRegistry.cs
    git commit -m "feat: add immutable system information service"

## Task 7: Integrate initialization, deterministic self-test, and AppModel markers

Files:
- Modify guideXOS/Program.cs.
- Modify guideXOS/OS/ApplicationServiceRegistry.cs.
- Modify guideXOS/OS/ApplicationInstance.cs.
- Modify run_uefi_validation.ps1.

Interfaces:
- Produces service initialization before factory use, ApplicationServiceRegistry.RunSelfTest(), bounded service counters, and APP_MODEL_SERVICES proof markers.

- [ ] Step 1: Add the failing AppModel gate.

    $serviceSelfTest = [regex]::Matches(
        $finalContent,
        '(?m)^APP_MODEL_SERVICES_SELFTEST_OK=1$').Count
    if ($isAppModelValidation -and $serviceSelfTest -lt 1) {
        $status = 'APP_MODEL_SERVICE_VALIDATION_FAILED'
    }

- [ ] Step 2: Run AppModel and verify the new gate fails because markers are absent.

    powershell -NoProfile -ExecutionPolicy Bypass -File .\run_uefi_validation.ps1 -AppModel -TimeoutSeconds 120 -SerialLog serial_phase8_service_gate_red.txt

- [ ] Step 3: Initialize services at the existing App Model boundary and call the self-test before APP_MODEL_COMPLETE. Service reset is limited to controlled initialization/diagnostic cleanup and never resets ApplicationInstanceRegistry authority.

    ApplicationServiceRegistry.Initialize();
    if (!ApplicationServiceRegistry.RunSelfTest()) {
        failure = "SERVICE_SELFTEST";
    } else {
        SerialBreadcrumb("APP_MODEL_SERVICES_SELFTEST_OK=1");
    }

- [ ] Step 4: Emit counters without changing existing fallback or legacy counters.

    SerialBreadcrumb("APP_MODEL_SERVICES_REGISTERED=" +
        ApplicationServiceRegistry.RegisteredCount.ToString());
    SerialBreadcrumb("APP_MODEL_SERVICES_DUPLICATE_REJECTED=" +
        (ApplicationServiceRegistry.DuplicateRegistrationRejected
            ? "1" : "0"));
    SerialBreadcrumb("APP_MODEL_SERVICES_STALE_REJECTED=" +
        ApplicationServiceRegistry.StaleContextRejections.ToString());
    SerialBreadcrumb("APP_MODEL_SERVICES_CLEANUP=" +
        (ApplicationServiceRegistry.DiagnosticsClean ? "1" : "0"));

- [ ] Step 5: Run AppModel and commit the integration slice.

    powershell -NoProfile -ExecutionPolicy Bypass -File .\run_uefi_validation.ps1 -AppModel -TimeoutSeconds 120 -SerialLog serial_phase8_appmodel_green.txt
    git add guideXOS/Program.cs guideXOS/OS/ApplicationServiceRegistry.cs guideXOS/OS/ApplicationInstance.cs run_uefi_validation.ps1
    git commit -m "test: integrate Phase 8 service self-test"

## Task 8: Migrate notification consumers through the service boundary (TDD)

Files:
- Modify guideXOS/OS/ApplicationFactories.cs.
- Modify guideXOS/DefaultApps/Calculator.cs.
- Modify guideXOS/GUI/DisplayOptions.cs.

Interfaces:
- Consumes ApplicationServiceContext, ApplicationServiceAccess, and IApplicationNotificationService.
- Produces migrated application fields/constructors with typed service access; no direct NotificationManager.Add in migrated paths.

- [ ] Step 1: Add a failing dependency assertion.

    $migrated = Get-Content guideXOS\DefaultApps\Calculator.cs,
        guideXOS\GUI\DisplayOptions.cs -Raw
    if ($migrated -match 'NotificationManager\.Add') {
        throw 'direct notification dependency remains'
    }

- [ ] Step 2: Run AppModel and confirm the dependency assertion fails before migration.

    powershell -NoProfile -ExecutionPolicy Bypass -File .\run_uefi_validation.ps1 -AppModel -TimeoutSeconds 120 -SerialLog serial_phase8_notification_migration_red.txt

- [ ] Step 3: Add a narrow factory helper that creates service context/access from the supplied instance and keeps the existing factory signature and ownership flow.

    ApplicationServiceContext context;
    ApplicationServiceAccess services;
    ApplicationServiceResult serviceResult;
    if (!ApplicationServiceRegistry.TryCreateContextAndAccess(
            instance.Handle, out context, out services,
            out serviceResult)) {
        result = ApplicationFactoryResult.Failed(
            LaunchErrorCode.InitializationFailed,
            serviceResult.BoundedDiagnostic, descriptor.AppId);
        return false;
    }

- [ ] Step 4: Replace only migrated notification calls with typed Publish, preserving wording/severity and avoiding exceptions in input/render loops.

    services.Notifications.Publish(
        context,
        ApplicationNotificationRequest.Create(
            "Calculator", message,
            ApplicationNotificationSeverity.Info));

- [ ] Step 5: Run AppModel, check migrated files, and commit.

    powershell -NoProfile -ExecutionPolicy Bypass -File .\run_uefi_validation.ps1 -AppModel -TimeoutSeconds 120 -SerialLog serial_phase8_notification_migration_green.txt
    rg -n 'NotificationManager\.Add' guideXOS\DefaultApps\Calculator.cs guideXOS\GUI\DisplayOptions.cs
    git add guideXOS/OS/ApplicationFactories.cs guideXOS/DefaultApps/Calculator.cs guideXOS/GUI/DisplayOptions.cs
    git commit -m "refactor: route app notifications through services"

Expected rg result: no matches in the two migrated files.

## Task 9: Migrate Notepad to application-scoped session settings (TDD)

Files:
- Modify guideXOS/OS/ApplicationFactories.cs.
- Modify guideXOS/DefaultApps/Notepad.cs.

Interfaces:
- Consumes ApplicationServiceContext, ApplicationServiceAccess.Settings, and ApplicationSettingValue.Boolean.
- Produces wrap preference access through the descriptor-keyed session namespace while document text, dirty flag, undo/redo, dialogs, and windows remain local.

- [ ] Step 1: Add a failing same-descriptor sharing test.

    settings.Set(notepadContext1, "wrap",
        ApplicationSettingValue.Boolean(false));
    ApplicationServiceResult<ApplicationSettingValue> shared =
        settings.Get(notepadContext2, "wrap");
    Check(shared.Succeeded && !shared.Value.BooleanValue,
        "Notepad settings shared by application identity",
        ref passed, ref failed, ref failure);

- [ ] Step 2: Run AppModel and verify the setting-sharing test fails while Notepad still owns wrap directly.

    powershell -NoProfile -ExecutionPolicy Bypass -File .\run_uefi_validation.ps1 -AppModel -TimeoutSeconds 120 -SerialLog serial_phase8_notepad_settings_red.txt

- [ ] Step 3: Pass context/access to Notepad and initialize missing wrap with the existing default true.

    private bool ReadWrapSetting() {
        ApplicationServiceResult<ApplicationSettingValue> result =
            _services.Settings.Get(_serviceContext, "wrap");
        return result.Succeeded &&
            result.Value.Kind == ApplicationSettingValueKind.Boolean
            ? result.Value.BooleanValue : true;
    }

- [ ] Step 4: Route the wrap toggle through Settings.Set while retaining only the current boolean as transient UI state.

    _wrap = !_wrap;
    _services.Settings.Set(
        _serviceContext, "wrap",
        ApplicationSettingValue.Boolean(_wrap));

- [ ] Step 5: Run AppModel and commit.

    powershell -NoProfile -ExecutionPolicy Bypass -File .\run_uefi_validation.ps1 -AppModel -TimeoutSeconds 120 -SerialLog serial_phase8_notepad_settings_green.txt
    git add guideXOS/OS/ApplicationFactories.cs guideXOS/DefaultApps/Notepad.cs
    git commit -m "refactor: route Notepad wrap preference through app settings"

## Task 10: Migrate Task Manager to immutable system snapshots (TDD)

Files:
- Modify guideXOS/OS/ApplicationFactories.cs.
- Modify guideXOS/DefaultApps/TaskManager.cs.

Interfaces:
- Consumes ApplicationServiceAccess.SystemInformation.GetSnapshot(context).
- Produces metrics sampling through copied snapshot values; rendering/chart state and observer behavior remain unchanged.

- [ ] Step 1: Add a failing dependency assertion for the migrated metrics path.

    $taskManager = Get-Content guideXOS\DefaultApps\TaskManager.cs -Raw
    if ($taskManager -match
        'Allocator\.(MemorySize|MemoryInUse)|ThreadPool\.CPUUsage|Timer\.Ticks') {
        throw 'raw system metric dependency remains in migrated path'
    }

- [ ] Step 2: Run AppModel and confirm the assertion fails before migration.

    powershell -NoProfile -ExecutionPolicy Bypass -File .\run_uefi_validation.ps1 -AppModel -TimeoutSeconds 120 -SerialLog serial_phase8_taskmanager_red.txt

- [ ] Step 3: Pass context/access to Task Manager and replace each bounded metrics sample with one snapshot request.

    ApplicationServiceResult<SystemInformationSnapshot> result =
        _services.SystemInformation.GetSnapshot(_serviceContext);
    if (!result.Succeeded) return;
    SystemInformationSnapshot snapshot = result.Value;
    _cpuUtilPct = snapshot.CpuUsagePercent;
    _threadCount = snapshot.ThreadCount;
    ulong totalMem = snapshot.MemorySizeBytes == 0
        ? 1UL : snapshot.MemorySizeBytes;
    ulong usedMem = snapshot.MemoryInUseBytes;

- [ ] Step 4: Run AppModel and the dependency check, then commit.

    powershell -NoProfile -ExecutionPolicy Bypass -File .\run_uefi_validation.ps1 -AppModel -TimeoutSeconds 120 -SerialLog serial_phase8_taskmanager_green.txt
    rg -n 'Allocator\.(MemorySize|MemoryInUse)|ThreadPool\.CPUUsage|Timer\.Ticks' guideXOS\DefaultApps\TaskManager.cs
    git add guideXOS/OS/ApplicationFactories.cs guideXOS/DefaultApps/TaskManager.cs
    git commit -m "refactor: route Task Manager metrics through system service"

Expected rg result: no matches in the migrated metrics path; unrelated observer dependencies must be reviewed before removal.

## Task 11: Add real application-instance service runtime diagnostic

Files:
- Modify guideXOS/OS/ApplicationInstance.cs.
- Modify guideXOS/OS/ApplicationFactories.cs.
- Modify guideXOS/Program.cs.
- Modify run_uefi_validation.ps1.

Interfaces:
- Produces ApplicationInstanceRegistry.RunApplicationServiceRuntimeDiagnostic(), APP_RUNTIME_SERVICES markers, and AppRuntime parser gates.

- [ ] Step 1: Add failing AppRuntime gates for notification, shared settings, immutable snapshot, stale rejection, and cleanup.

    $serviceRuntime = [regex]::Matches(
        $finalContent,
        '(?m)^APP_RUNTIME_SERVICES_RESULT=PASS$').Count
    if ($isAppRuntimeValidation -and $serviceRuntime -lt 1) {
        $status = 'APP_RUNTIME_SERVICE_VALIDATION_FAILED'
    }

- [ ] Step 2: Run AppRuntime and verify the new marker is absent while existing gates remain green.

    powershell -NoProfile -ExecutionPolicy Bypass -File .\run_uefi_validation.ps1 -AppRuntime -TimeoutSeconds 180 -SerialLog serial_phase8_service_runtime_red.txt

- [ ] Step 3: Implement the diagnostic with normal LaunchRequest/factory paths. Launch Calculator, Notepad twice, and Task Manager; publish a notification, set/read Notepad wrap through both contexts, request a system snapshot, terminate one instance, exercise the stale context, and close every diagnostic instance through the existing lifecycle API.

    bool calculator = TryLaunchDiagnostic(
        "gxos.builtin.calculator", out calcInstance);
    bool notepad1 = TryLaunchDiagnostic(
        "gxos.builtin.notepad", out note1);
    bool notepad2 = TryLaunchDiagnostic(
        "gxos.builtin.notepad", out note2);
    bool taskManager = TryLaunchDiagnostic(
        "gxos.builtin.taskmanager", out taskInstance);

    bool shared = settings.Set(
            note1Context, "wrap",
            ApplicationSettingValue.Boolean(false)).Succeeded &&
        !settings.Get(note2Context, "wrap").Value.BooleanValue;
    bool snapshot =
        taskServices.SystemInformation.GetSnapshot(taskContext).Succeeded;

    ApplicationServiceContext stale = note1Context;
    ApplicationInstanceRegistry.TryTerminate(
        note1.Handle, ApplicationCloseReason.ApplicationRequest,
        out closeResult);
    bool staleRejected =
        settings.Get(stale, "wrap").Code ==
        ApplicationServiceResultCode.InvalidContext;

- [ ] Step 4: Emit bounded markers and require all cleanup counters to return to their pre-diagnostic baselines.

    Program.MarkUefiAppRuntime(
        "APP_RUNTIME_SERVICES_SHARED_SETTINGS=" +
        (shared ? "PASS" : "FAIL"));
    Program.MarkUefiAppRuntime(
        "APP_RUNTIME_SERVICES_SNAPSHOT=" +
        (snapshot ? "PASS" : "FAIL"));
    Program.MarkUefiAppRuntime(
        "APP_RUNTIME_SERVICES_STALE_REJECTED=" +
        (staleRejected ? "PASS" : "FAIL"));
    Program.MarkUefiAppRuntime(
        "APP_RUNTIME_SERVICES_RESULT=" +
        (cleanup && notification && shared && snapshot &&
            staleRejected ? "PASS" : "FAIL"));

- [ ] Step 5: Run AppRuntime and commit.

    powershell -NoProfile -ExecutionPolicy Bypass -File .\run_uefi_validation.ps1 -AppRuntime -TimeoutSeconds 180 -SerialLog serial_phase8_service_runtime_green.txt
    git add guideXOS/OS/ApplicationInstance.cs guideXOS/OS/ApplicationFactories.cs guideXOS/Program.cs run_uefi_validation.ps1
    git commit -m "test: prove application service runtime lifecycle safety"

## Task 12: Complete canonical documentation and convergence matrix

Files:
- Modify APP_MODEL_CONVERGENCE.md.

Interfaces:
- Consumes audit evidence, contracts, backend behavior, migrated apps, self-test markers, and runtime logs.
- Produces the canonical Phase 8 architecture section and final service convergence matrix.

- [ ] Step 1: Document service access, context fields, fixed registry, typed results, service-specific lifecycle eligibility, application-scoped session settings, copied system snapshots, capability behavior, C# backends, Server semantics, migrated applications, and deferred services.

    ## Application platform services

    ### Service access model
    Describe the fixed registry, validated context, typed access, and service-specific lifecycle masks.

    ### First-cohort convergence matrix
    Record every audited service with Server source, C# source, classification, semantics, backend difference, migration value, risk, and decision.

    ### Runtime proof and deferred services
    Record fresh self-test/runtime evidence and the explicit deferral of dialogs, app-local storage, resources, clipboard, and shell/open services.

- [ ] Step 2: Add actual source locations and observed classifications from Task 1. Do not claim Server capability enforcement or persistence that the audit did not find.

- [ ] Step 3: Record exact runtime marker names and acceptance requirements, preserving all Phase 7 requirements and adding APP_MODEL_SERVICES_SELFTEST_OK=1, APP_RUNTIME_SERVICES_RESULT=PASS, and stale service contexts=0.

- [ ] Step 4: Check and commit.

    git diff --check
    git add APP_MODEL_CONVERGENCE.md
    git commit -m "docs: document Phase 8 application services"

## Task 13: Run the complete Phase 7 regression matrix and focused service checks

Files:
- Read all changed files and validation logs.
- Modify none unless a verified regression requires a focused fix cycle.

Interfaces:
- Consumes the completed Phase 8 implementation and canonical documentation.
- Produces fresh evidence for every required gate and final report data.

- [ ] Step 1: Run a release build with the AppModel diagnostic selector.

    powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -UefiDiagnosticMode AppModel

- [ ] Step 2: Run AppModel and verify 12 descriptors/factories, fallback 0, legacy backend 0, compatibility gates, service self-test, and service cleanup.

    powershell -NoProfile -ExecutionPolicy Bypass -File .\run_uefi_validation.ps1 -AppModel -SkipBuild -TimeoutSeconds 120 -SerialLog serial_phase8_final_appmodel.txt

- [ ] Step 3: Run AppRuntime and verify service runtime, lifecycle/taskbar/grouping, associations, GXM, installer, and cleanup gates.

    powershell -NoProfile -ExecutionPolicy Bypass -File .\run_uefi_validation.ps1 -AppRuntime -SkipBuild -TimeoutSeconds 240 -SerialLog serial_phase8_final_appruntime.txt

- [ ] Step 4: Run NativeInput, ContextMenu, and production Continuous validations.

    powershell -NoProfile -ExecutionPolicy Bypass -File .\run_uefi_validation.ps1 -NativeInput -TimeoutSeconds 180 -SerialLog serial_phase8_final_nativeinput.txt
    powershell -NoProfile -ExecutionPolicy Bypass -File .\run_uefi_validation.ps1 -ContextMenu -TimeoutSeconds 180 -SerialLog serial_phase8_final_contextmenu.txt
    powershell -NoProfile -ExecutionPolicy Bypass -File .\run_uefi_validation.ps1 -Continuous -TimeoutSeconds 300 -SerialLog serial_phase8_final_continuous.txt

- [ ] Step 5: Parse mandatory invariants from fresh logs.

    rg -n 'APP_MODEL_COMPLETE|APP_MODEL_SERVICES_SELFTEST_OK=1|APP_RUNTIME_COMPLETE|APP_RUNTIME_SERVICES_RESULT=PASS|APP_MODEL_FACTORY_FALLBACKS=0|APP_MODEL_COMPAT_LEGACY_BACKEND_CALLS=0|ALLOCATOR_CORRUPT=0|ThreadPool\.Locked=0|GRAPHICS_VALID=1|unexpected input drops=0|stale instances=0|stale ownership=0|stale taskbar owners=0|stale service contexts=0' serial_phase8_final_*.txt

- [ ] Step 6: If a focused check fails, add a reproducing self-test first, run it red, make the smallest fix, rerun the focused gate, and only then rerun the full matrix. Do not reset, clean, stash, rebase, or discard unrelated work.

- [ ] Step 7: Check final state and commit only verified fixes/evidence.

    git diff --check
    git status --short --branch
    git log --oneline --decorate -8
    git commit -m "test: accept Phase 8 application services"

## Final verification checklist

- [ ] Server/C# inventory is complete and source-located.
- [ ] Dialogs, app-local storage, resources, clipboard, and shell/open services are classified and documented as deferred.
- [ ] Fixed service registry has deterministic IDs and rejects duplicates/missing services.
- [ ] Context creation and every service call reject stale/terminated/failed/closing/suspended instances.
- [ ] Service-specific lifecycle eligibility is narrower than the common validator where appropriate.
- [ ] Settings are shared by descriptor ID across two instances and isolated between descriptors.
- [ ] Settings are session-only and bounded; no persistence is claimed.
- [ ] System information is an immutable copied snapshot with bounded text and scalar fields.
- [ ] Migrated applications no longer reach the selected service backends directly.
- [ ] Notepad transient document/undo/dialog state remains instance-local.
- [ ] Task Manager rendering/chart/observation state remains application-local.
- [ ] Service self-test and runtime service diagnostic pass with full cleanup.
- [ ] AppModel, compatibility, lifecycle, taskbar/grouping, AppRuntime, NativeInput, ContextMenu, and production Continuous pass.
- [ ] Fallback 0, legacy backend 0, allocator corruption 0, ThreadPool.Locked=0, valid graphics, zero unexpected input drops, stale instances 0, stale ownership 0, stale taskbar entries 0, and stale service contexts 0 are evidenced by fresh logs.
- [ ] Only D:\dev\guideXOSUEFI changed; Server and Legacy remain untouched.
