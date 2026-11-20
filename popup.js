"use strict";

const HOST_NAME = "com.extension_folder_locator.host";

function installPreviewApi() {
  if (location.protocol !== "file:") {
    return;
  }
  if (typeof chrome !== "undefined" && chrome.management && chrome.runtime && chrome.runtime.id) {
    return;
  }

  const previewOnline = new URLSearchParams(location.search).get("preview") === "online";
  const events = { addListener: () => {} };
  const previewExtensions = [
    ["afpbjjgbdimpioenaedcjgkaigggcdpp", "QR Code Generator", "11.3.2", true],
    ["bpoadfkcbjbfhfodiogcnhhhpibjhbnh", "Immersive Translate", "1.34.4", true],
    ["elicpjhcidhpjomhibiffojpinpmmpil", "Video Downloader Professional", "2.1.7", true],
    ["hihblcmlaaademjlakdpicchbjnnnkbo", "JSON Viewer Pro", "3.0.5", true],
    ["kbajefidbakalojflbaojijekaomlidj", "密码管理器", "3.2.0", true],
    ["medapdbncneneejhbgcjceippjlfkmkg", "网页截图工具", "3.6.33", false],
    ["nmmhkkegccagdldgiimedpiccmgmieda", "Chrome Web Store 支付", "1.0.0.6", true],
    ["plhnjpnbkmdmooideifhkonobdkgbbof", "开发者效率面板", "2026.11.3", true]
  ].map(([id, name, version, enabled]) => ({
    description: "",
    enabled,
    icons: [],
    id,
    name,
    type: "extension",
    version
  }));

  const previewChrome = {
    runtime: {
      id: "pppppppppppppppppppppppppppppppp",
      lastError: null,
      sendNativeMessage: (host, message, callback) => {
        window.setTimeout(() => {
          if (previewOnline) {
            callback({
              ok: true,
              host: "ExtensionFolderLocator",
              version: "1.0.0"
            });
          } else {
            previewChrome.runtime.lastError = {
              message: "Specified native messaging host not found."
            };
            callback(undefined);
            previewChrome.runtime.lastError = null;
          }
        }, 80);
      }
    },
    management: {
      getAll: (callback) => window.setTimeout(() => callback(previewExtensions), 60),
      onDisabled: events,
      onEnabled: events,
      onInstalled: events,
      onUninstalled: events
    }
  };

  globalThis.chrome = previewChrome;
}

installPreviewApi();

const elements = {
  countBadge: document.getElementById("countBadge"),
  copyInstallButton: document.getElementById("copyInstallButton"),
  copyPathButton: document.getElementById("copyPathButton"),
  dismissSetupButton: document.getElementById("dismissSetupButton"),
  emptyDescription: document.getElementById("emptyDescription"),
  emptyState: document.getElementById("emptyState"),
  emptyTitle: document.getElementById("emptyTitle"),
  extensionGrid: document.getElementById("extensionGrid"),
  hostStatus: document.getElementById("hostStatus"),
  hostStatusText: document.getElementById("hostStatusText"),
  loadingState: document.getElementById("loadingState"),
  locatedPath: document.getElementById("locatedPath"),
  locatedPathText: document.getElementById("locatedPathText"),
  openButton: document.getElementById("openButton"),
  refreshButton: document.getElementById("refreshButton"),
  searchInput: document.getElementById("searchInput"),
  selectedId: document.getElementById("selectedId"),
  selectedName: document.getElementById("selectedName"),
  selectedVersion: document.getElementById("selectedVersion"),
  setupBanner: document.getElementById("setupBanner"),
  summaryText: document.getElementById("summaryText"),
  toast: document.getElementById("toast"),
  toggleFilterButton: document.getElementById("toggleFilterButton")
};

const state = {
  extensions: [],
  enabledOnly: false,
  hostOnline: false,
  lastLocatedPath: "",
  query: "",
  selectedId: null,
  setupDismissed: false
};

let toastTimer = null;

function getInstalledExtensions() {
  return new Promise((resolve, reject) => {
    chrome.management.getAll((items) => {
      const runtimeError = chrome.runtime.lastError;
      if (runtimeError) {
        reject(new Error(runtimeError.message));
        return;
      }

      resolve(items.filter((item) => item.type === "extension"));
    });
  });
}

function detectBrowserHint() {
  const userAgent = navigator.userAgent;
  if (navigator.brave) {
    return "Brave";
  }
  if (userAgent.includes("Edg/")) {
    return "Edge";
  }
  if (userAgent.includes("Chromium/") && !userAgent.includes("Chrome/")) {
    return "Chromium";
  }
  return "Chrome";
}

function sendNativeMessage(payload) {
  return new Promise((resolve, reject) => {
    chrome.runtime.sendNativeMessage(HOST_NAME, payload, (response) => {
      const runtimeError = chrome.runtime.lastError;
      if (runtimeError) {
        reject(new Error(runtimeError.message));
        return;
      }
      if (!response) {
        reject(new Error("本机助手没有返回结果"));
        return;
      }
      resolve(response);
    });
  });
}

function setHostStatus(status, text) {
  elements.hostStatus.className = `host-status ${status}`;
  elements.hostStatusText.textContent = text;
}

async function checkHost(showSuccessToast) {
  state.hostOnline = false;
  setHostStatus("checking", "检测中");

  try {
    const response = await sendNativeMessage({ action: "ping" });
    if (!response.ok) {
      throw new Error(response.error || "本机助手异常");
    }

    state.hostOnline = true;
    setHostStatus("online", "助手已连接");
    elements.setupBanner.hidden = true;
    if (showSuccessToast) {
      showToast(`本机助手 v${response.version} 已连接`);
    }
    return true;
  } catch (error) {
    state.hostOnline = false;
    setHostStatus("offline", "助手未连接");
    elements.setupBanner.hidden = state.setupDismissed;
    if (showSuccessToast) {
      showToast("未检测到本机助手，请先运行安装脚本", true);
    }
    return false;
  }
}

function getVisibleExtensions() {
  const normalizedQuery = state.query.trim().toLocaleLowerCase();
  return state.extensions
    .filter((item) => !state.enabledOnly || item.enabled)
    .filter((item) => {
      if (!normalizedQuery) {
        return true;
      }
      return (
        item.name.toLocaleLowerCase().includes(normalizedQuery) ||
        item.id.toLocaleLowerCase().includes(normalizedQuery)
      );
    })
    .sort((left, right) => {
      if (left.enabled !== right.enabled) {
        return left.enabled ? -1 : 1;
      }
      return left.name.localeCompare(right.name, "zh-CN");
    });
}

function iconUrlFor(extension) {
  if (!Array.isArray(extension.icons) || extension.icons.length === 0) {
    return "";
  }

  const sorted = [...extension.icons].sort((left, right) => right.size - left.size);
  return sorted[0].url || "";
}

function colorHue(id) {
  let hash = 0;
  for (let index = 0; index < id.length; index += 1) {
    hash = (hash * 31 + id.charCodeAt(index)) % 360;
  }
  return hash;
}

function makeExtensionCard(extension) {
  const card = document.createElement("button");
  card.type = "button";
  card.className = "extension-card";
  card.dataset.id = extension.id;
  card.setAttribute("role", "option");
  card.setAttribute("aria-selected", extension.id === state.selectedId ? "true" : "false");
  card.title = `${extension.name}\n${extension.id}`;

  if (extension.id === state.selectedId) {
    card.classList.add("selected");
  }
  if (!extension.enabled) {
    card.classList.add("disabled");
  }

  const top = document.createElement("div");
  top.className = "card-top";

  const icon = document.createElement("div");
  icon.className = "extension-icon";
  icon.style.setProperty("--hue", colorHue(extension.id));
  const iconUrl = iconUrlFor(extension);
  if (iconUrl) {
    const image = document.createElement("img");
    image.src = iconUrl;
    image.alt = "";
    image.addEventListener("error", () => {
      image.remove();
      icon.textContent = extension.name.slice(0, 1).toLocaleUpperCase();
    });
    icon.appendChild(image);
  } else {
    icon.textContent = extension.name.slice(0, 1).toLocaleUpperCase();
  }

  const extensionState = document.createElement("span");
  extensionState.className = `extension-state${extension.enabled ? " enabled" : ""}`;
  extensionState.title = extension.enabled ? "已启用" : "已停用";
  top.append(icon, extensionState);

  const name = document.createElement("div");
  name.className = "extension-name";
  name.textContent = extension.name;

  const meta = document.createElement("div");
  meta.className = "extension-meta";

  const version = document.createElement("span");
  version.textContent = `v${extension.version || "未知"}`;

  const divider = document.createElement("span");
  divider.className = "meta-divider";

  const id = document.createElement("span");
  id.textContent = extension.id.slice(0, 8);

  meta.append(version, divider, id);

  const check = document.createElement("span");
  check.className = "selected-check";
  check.textContent = "✓";

  card.append(top, name, meta, check);
  card.addEventListener("click", () => selectExtension(extension.id));
  return card;
}

function renderExtensions() {
  const visible = getVisibleExtensions();
  elements.extensionGrid.replaceChildren(...visible.map(makeExtensionCard));
  elements.countBadge.textContent = String(visible.length);

  const enabledCount = state.extensions.filter((item) => item.enabled).length;
  elements.summaryText.textContent = state.extensions.length
    ? `${enabledCount} 个已启用 · ${state.extensions.length} 个已安装`
    : "暂无已安装插件";

  const hasNoResults = visible.length === 0;
  elements.emptyState.hidden = !hasNoResults;
  elements.extensionGrid.hidden = hasNoResults;

  if (hasNoResults) {
    const isSearchEmpty = Boolean(state.query.trim());
    elements.emptyTitle.textContent = isSearchEmpty ? "没有匹配的插件" : "没有符合条件的插件";
    elements.emptyDescription.textContent = state.enabledOnly
      ? "当前只显示已启用的插件，可以点击右上角切换筛选。"
      : "试试更换搜索关键词。";
  }
}

function updateSelectionPanel() {
  const selected = state.extensions.find((item) => item.id === state.selectedId);
  if (!selected) {
    elements.selectedName.textContent = "选择一个插件";
    elements.selectedId.textContent = "选择后可查看安装位置";
    elements.selectedVersion.hidden = true;
    elements.openButton.disabled = true;
    elements.locatedPath.hidden = true;
    return;
  }

  elements.selectedName.textContent = selected.name;
  elements.selectedId.textContent = selected.id;
  elements.selectedVersion.textContent = `v${selected.version || "未知"}`;
  elements.selectedVersion.hidden = false;
  elements.openButton.disabled = false;

  if (state.lastLocatedPath && state.lastLocatedPathExtensionId === selected.id) {
    elements.locatedPathText.textContent = state.lastLocatedPath;
    elements.locatedPath.title = state.lastLocatedPath;
    elements.locatedPath.hidden = false;
  } else {
    elements.locatedPath.hidden = true;
  }
}

function selectExtension(extensionId) {
  state.selectedId = extensionId;
  if (state.lastLocatedPathExtensionId !== extensionId) {
    state.lastLocatedPath = "";
  }
  renderExtensions();
  updateSelectionPanel();
  const selectedCard = elements.extensionGrid.querySelector(`[data-id="${extensionId}"]`);
  selectedCard?.scrollIntoView({ block: "nearest" });
}

async function loadExtensions() {
  elements.refreshButton.classList.add("is-loading");
  elements.loadingState.hidden = false;
  elements.extensionGrid.hidden = true;

  try {
    const extensions = await getInstalledExtensions();
    state.extensions = extensions;

    if (state.selectedId && !extensions.some((item) => item.id === state.selectedId)) {
      state.selectedId = null;
    }

    elements.loadingState.hidden = true;
    renderExtensions();
    updateSelectionPanel();
  } catch (error) {
    elements.loadingState.hidden = true;
    elements.emptyState.hidden = false;
    elements.emptyTitle.textContent = "无法读取插件列表";
    elements.emptyDescription.textContent = error.message;
  } finally {
    elements.refreshButton.classList.remove("is-loading");
  }
}

async function openSelectedFolder() {
  const selected = state.extensions.find((item) => item.id === state.selectedId);
  if (!selected) {
    return;
  }

  if (!state.hostOnline && !(await checkHost(false))) {
    showToast("请先安装本机助手，然后点击“助手未连接”重试", true);
    return;
  }

  elements.openButton.disabled = true;
  elements.openButton.classList.add("busy");
  elements.openButton.querySelector("span").textContent = "正在定位";

  try {
    const response = await sendNativeMessage({
      action: "open",
      browserHint: detectBrowserHint(),
      extensionId: selected.id,
      extensionName: selected.name
    });

    if (!response.ok) {
      throw new Error(response.error || "无法打开插件文件夹");
    }
    if (!response.found) {
      showToast(response.message || "未找到该插件的安装文件夹", true);
      return;
    }

    state.lastLocatedPath = response.path;
    state.lastLocatedPathExtensionId = selected.id;
    elements.locatedPathText.textContent = response.path;
    elements.locatedPath.title = response.path;
    elements.locatedPath.hidden = false;

    const profileSuffix = response.profile ? `（${response.profile}）` : "";
    const alternativeCount = Array.isArray(response.alternatives) ? response.alternatives.length : 0;
    const alternativeSuffix = alternativeCount > 0 ? `，另有 ${alternativeCount} 个配置位置` : "";
    showToast(`已打开文件夹${profileSuffix}${alternativeSuffix}`);
  } catch (error) {
    if (/native messaging host|not found|指定的程序|找不到/i.test(error.message)) {
      state.hostOnline = false;
      setHostStatus("offline", "助手未连接");
      elements.setupBanner.hidden = false;
    }
    showToast(error.message, true);
  } finally {
    elements.openButton.disabled = false;
    elements.openButton.classList.remove("busy");
    elements.openButton.querySelector("span").textContent = "打开文件夹";
  }
}

function showToast(message, isError) {
  window.clearTimeout(toastTimer);
  elements.toast.textContent = message;
  elements.toast.classList.toggle("error", Boolean(isError));
  elements.toast.classList.add("show");
  toastTimer = window.setTimeout(() => {
    elements.toast.classList.remove("show");
  }, 3200);
}

async function copyText(text, successMessage) {
  try {
    await navigator.clipboard.writeText(text);
    showToast(successMessage);
  } catch (error) {
    showToast("复制失败，请手动选择文本", true);
  }
}

function copyInstallCommand() {
  const command = `powershell -ExecutionPolicy Bypass -File .\\install.ps1 -ExtensionId ${chrome.runtime.id}`;
  copyText(command, "安装命令已复制，请在项目目录中运行");
}

function copyLocatedPath() {
  if (state.lastLocatedPath) {
    copyText(state.lastLocatedPath, "安装路径已复制");
  }
}

function bindEvents() {
  elements.refreshButton.addEventListener("click", loadExtensions);
  elements.hostStatus.addEventListener("click", () => checkHost(true));
  elements.openButton.addEventListener("click", openSelectedFolder);
  elements.copyInstallButton.addEventListener("click", copyInstallCommand);
  elements.copyPathButton.addEventListener("click", copyLocatedPath);
  elements.dismissSetupButton.addEventListener("click", () => {
    state.setupDismissed = true;
    elements.setupBanner.hidden = true;
  });

  elements.searchInput.addEventListener("input", (event) => {
    state.query = event.target.value;
    renderExtensions();
  });

  elements.toggleFilterButton.addEventListener("click", () => {
    state.enabledOnly = !state.enabledOnly;
    elements.toggleFilterButton.textContent = state.enabledOnly ? "显示全部" : "仅看已启用";
    renderExtensions();
  });

  document.addEventListener("keydown", (event) => {
    if ((event.ctrlKey || event.metaKey) && event.key.toLocaleLowerCase() === "k") {
      event.preventDefault();
      elements.searchInput.focus();
      elements.searchInput.select();
    }
    if (event.key === "Enter" && document.activeElement === elements.searchInput) {
      const firstResult = getVisibleExtensions()[0];
      if (firstResult) {
        selectExtension(firstResult.id);
      }
    }
  });

  const managementEvents = [
    chrome.management.onEnabled,
    chrome.management.onDisabled,
    chrome.management.onInstalled,
    chrome.management.onUninstalled
  ];
  for (const event of managementEvents) {
    event.addListener(loadExtensions);
  }
}

async function init() {
  bindEvents();
  await Promise.all([loadExtensions(), checkHost(false)]);
}

init();
