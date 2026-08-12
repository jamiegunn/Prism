/**
 * Points @monaco-editor/react at the locally-bundled monaco-editor package instead of the
 * jsdelivr CDN it loads from by default. Prism is built to run against local inference on
 * machines that may have no internet at all — an editor that needs a CDN would hang at
 * "Loading..." forever exactly where the app is meant to shine. Importing this module once,
 * before the first <Editor> renders, makes the editors work offline.
 */
import * as monaco from 'monaco-editor'
import editorWorker from 'monaco-editor/esm/vs/editor/editor.worker?worker'
import htmlWorker from 'monaco-editor/esm/vs/language/html/html.worker?worker'
import jsonWorker from 'monaco-editor/esm/vs/language/json/json.worker?worker'
import { loader } from '@monaco-editor/react'

// Monaco spawns web workers for tokenisation and language services. Handlebars templates
// ride the HTML language service, so it needs the html worker (without it every keystroke
// logs "Missing requestHandler" errors); json covers schema editors; everything else falls
// back to the base editor worker.
self.MonacoEnvironment = {
  getWorker: (_workerId: string, label: string) => {
    if (label === 'html' || label === 'handlebars' || label === 'razor') {
      return new htmlWorker()
    }

    if (label === 'json') {
      return new jsonWorker()
    }

    return new editorWorker()
  },
}

loader.config({ monaco })
