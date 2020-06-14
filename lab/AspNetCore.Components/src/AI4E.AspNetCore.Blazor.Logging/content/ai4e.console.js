if (typeof window !== 'undefined' && !window.ai4e) {
    window.ai4e = {};
}

window.ai4e.console = {
    log: function (message, foreground, background) {
        if (foreground === undefined && background === undefined) {
            console.log(message);
            return;
        }

        let css = "";

        if (foreground !== undefined) {
            css += "color: " + foreground + ";";
        }

        if (background !== undefined) {
            css += "background: " + background + ";";
        }

        console.log("%c" + message, css);
    }
};
