// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

import 'bootstrap'
import { enableAnchor } from './scripts/anchor'
import { highlight } from './scripts/highlight'
import { renderAlerts, renderLinks, renderTables, renderTabs } from './scripts/markdown'
import { renderAside, renderNavbar } from './scripts/nav'
import { enableSwitchTheme } from './scripts/theme'
import { renderToc } from './scripts/toc'

document.addEventListener('DOMContentLoaded', onContentLoad)

async function renderGitHubContributors() {
  const a = await fetch('https://github.com/Microsoft/vscode/contributors-list/main/README.md')
  console.log(a)
}

function onContentLoad() {
  renderGitHubContributors()

  enableAnchor()
  enableSwitchTheme()
  highlight()

  renderTables()
  renderAlerts()
  renderLinks()

  renderNavbar()
  renderToc()

  renderAside()
  renderTabs()
}