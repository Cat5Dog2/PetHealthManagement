// CSP で inline handler を禁止しているため、確認ダイアログや自動送信は
// data 属性 + イベントデリゲーションで実装する。

// data-confirm: フォーム送信前に確認ダイアログを表示する
document.addEventListener('submit', function (event) {
    const form = event.target;
    if (!(form instanceof HTMLFormElement)) {
        return;
    }

    const message = form.dataset.confirm;
    if (message && !window.confirm(message)) {
        event.preventDefault();
    }
});

// data-autosubmit: select / checkbox の変更で属するフォームを送信する
document.addEventListener('change', function (event) {
    const element = event.target;
    if (element instanceof HTMLElement && element.dataset.autosubmit !== undefined && element.form) {
        element.form.submit();
    }
});

// ステータスアラートは数秒後に自動で閉じる（×ボタンでも閉じられる）
document.addEventListener('DOMContentLoaded', function () {
    const alertElement = document.querySelector('.app-status-alert');
    if (!alertElement || typeof bootstrap === 'undefined') {
        return;
    }

    window.setTimeout(function () {
        bootstrap.Alert.getOrCreateInstance(alertElement).close();
    }, 5000);
});
